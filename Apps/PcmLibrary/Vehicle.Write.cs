﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PcmHacking
{
    public partial class Vehicle
    {
        /// <summary>
        /// Replace the full contents of the PCM.
        /// </summary>
        public async Task<bool> Write(bool fullWrite, bool kernelRunning, bool recoveryMode, CancellationToken cancellationToken, Stream stream)
        {
            try
            {
                this.device.ClearMessageQueue();

                if (!kernelRunning)
                {
                    // switch to 4x, if possible. But continue either way.
                    // if the vehicle bus switches but the device does not, the bus will need to time out to revert back to 1x, and the next steps will fail.
//                    if (!await this.VehicleSetVPW4x(VpwSpeed.FourX))
//                    {
//                        this.logger.AddUserMessage("Stopping here because we were unable to switch to 4X.");
//                        return false;
//                    }

                    Response<byte[]> response = await LoadKernelFromFile("write-kernel.bin");
                    if (response.Status != ResponseStatus.Success)
                    {
                        logger.AddUserMessage("Failed to load kernel from file.");
                        return false;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return false;
                    }

                    // TODO: instead of this hard-coded address, get the base address from the PcmInfo object.
                    if (!await PCMExecute(response.Value, 0xFF81FE, cancellationToken))
                    {
                        logger.AddUserMessage("Failed to upload kernel to PCM");

                        return false;
                    }

//                    await toolPresentNotifier.Notify();

                    logger.AddUserMessage("Kernel uploaded to PCM succesfully.");
                }

                await this.device.SetTimeout(TimeoutScenario.Maximum);

                if (fullWrite)
                {
                    await this.FullWrite(cancellationToken, stream);
                }
                else
                {
                    await this.CalibrationWrite(cancellationToken, stream);
                }

                return true;
            }
            catch (Exception exception)
            {
                this.logger.AddUserMessage("Something went wrong. " + exception.Message);
                this.logger.AddDebugMessage(exception.ToString());
                return false;
            }
            finally
            {
//                await TryWriteKernelReset();
//                await this.Cleanup();
            }
        }
        
        private async Task FullWrite(CancellationToken cancellationToken, Stream stream)
        {
            Message start = new Message(new byte[] { 0x6C, 0x10, 0xF0, 0x3C, 0x01 });

            if (!await this.SendMessageValidateResponse(
                start,
                this.messageParser.ParseStartFullFlashResponse,
                "start full flash",
                "Full flash starting.",
                "Kernel won't allow a full flash."))
            {
                return;
            }
            
            byte chunkSize = 192;
            byte[] header = new byte[] { 0x6D, 0x10, 0x0F0, 0x36, 0x00, 0x00, chunkSize, 0xFF, 0xA0, 0x00 };
            byte[] messageBytes = new byte[header.Length + chunkSize + 2];
            Buffer.BlockCopy(header, 0, messageBytes, 0, header.Length);
            for (int bytesSent = 0; bytesSent < stream.Length; bytesSent += chunkSize)
            {
                int bytesRead = stream.Read(messageBytes, header.Length, chunkSize);
                VPWUtils.AddBlockChecksum(messageBytes); // TODO: Move this function into the Message class.
                Message message = new Message(messageBytes);

                if (!await this.SendMessageValidateResponse(
                    message,
                    this.messageParser.ParseChunkWriteResponse,
                    string.Format("data from {0} to {1}", bytesSent, bytesSent + chunkSize),
                    "Data chunk sent.",
                    "Unable to send data chunk."))
                {
                    return;
                }
            }
        }

        private Task CalibrationWrite(CancellationToken cancellationToken, Stream stream)
        {
            return Task.FromResult(0);
        }

        /// <summary>
        /// Write a 16 bit sum to the end of a block, returns a Message, as a byte array
        /// </summary>
        /// <remarks>
        /// This is duplicating the code in MessageFactory. Should get rid of this and just use that.
        /// </remarks>
        public static UInt16 CalcBlockChecksum(byte[] Block)
        {
            UInt16 Sum = 0;
            int PayloadLength = (Block[5] << 8) + Block[6];

            int start = 4;
            int end = Block.Length - 2;

            for (int i = start; i < end; i++) // skip prio, dest, src, mode
            {
                Sum += Block[i];
            }

            return Sum;
        }

        /// <summary>
        /// Write a 16 bit sum to the end of a block, returns a Message, as a byte array
        /// </summary>
        /// <remarks>
        /// 
        /// This is duplicating the code in MessageFactory. Should get rid of this and just use that.
        /// 
        /// Appends 2 bytes at the end of the array with the sum
        /// TODO: Throw an error if the input data is not valid?
        /// 
        /// 6C|10|F0|36/80|03 F1|FF 91 50 .... CA CS
        /// 0  1  2  3  4  5  6  7  8  9
        /// 1  2  3  4  5  6  7  8  9  10      11 12
        /// </remarks>
        public static byte[] AddBlockChecksum(byte[] Block)
        {
            UInt16 Sum = 0;
            int PayloadLength;

            // Only generate the sum and append to the block if the length is right
            if (Block.Length > 6) // Do we have a length?
            {
                PayloadLength = (Block[5] << 8) + Block[6];
                if (Block.Length == PayloadLength + 12) // Correct block size?
                {
                    Sum = CalcBlockChecksum(Block);

                    Block[Block.Length - 2] = unchecked((byte)(Sum >> 8));
                    Block[Block.Length - 1] = unchecked((byte)(Sum & 0xFF));

                    return Block;
                }
            }
            return Block;
        }

        private async Task<bool> SendMessageValidateResponse(
            Message message,
            Func<Message, Response<bool>> filter,
            string messageDescription,
            string successMessage,
            string failureMessage,
            int maxAttempts = 5,
            bool pingKernel = false)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                this.logger.AddUserMessage("Sending " + messageDescription);

                if (!await this.TrySendMessage(message, messageDescription, maxAttempts))
                {
                    this.logger.AddUserMessage("Unable to send " + messageDescription);
                    if (pingKernel)
                    {
                        await this.TryWaitForKernel(1);
                    }
                    continue;
                }

                if (!await this.WaitForSuccess(filter, 10))
                {
                    this.logger.AddUserMessage("No " + messageDescription + " response received.");
                    if (pingKernel)
                    {
                        await this.TryWaitForKernel(1);
                    }
                    continue;
                }

                this.logger.AddUserMessage(successMessage);
                return true;
            }

            this.logger.AddUserMessage(failureMessage);
            if (pingKernel)
            {
                await this.TryWaitForKernel(1);
            }
            return false;
        }
    }
}
