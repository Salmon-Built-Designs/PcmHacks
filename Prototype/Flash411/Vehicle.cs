﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flash411
{
    /// <summary>
    /// From the application's perspective, this class is the API to the vehicle.
    /// </summary>
    /// <remarks>
    /// Methods in this class are high-level operations like "get the VIN," or "read the contents of the EEPROM."
    /// </remarks>
    class Vehicle : IDisposable
    {
        /// <summary>
        /// The device we'll use to talk to the PCM.
        /// </summary>
        private Device device;

        /// <summary>
        /// This class knows how to generate message to send to the PCM.
        /// </summary>
        private MessageFactory messageFactory;

        /// <summary>
        /// This class knows how to parse the messages that come in from the PCM.
        /// </summary>
        private MessageParser messageParser;

        /// <summary>
        /// This is how we send user-friendly status messages and developer-oriented debug messages to the UI.
        /// </summary>
        private ILogger logger;

        public string DeviceDescription
        {
            get
            {
                return this.device.ToString();
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public Vehicle(
            Device device, 
            MessageFactory messageFactory,
            MessageParser messageParser,
            ILogger logger)
        {
            this.device = device;
            this.messageFactory = messageFactory;
            this.messageParser = messageParser;
            this.logger = logger;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~Vehicle()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Implements IDisposable.Dispose.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// Part of the Dispose pattern.
        /// </summary>
        protected void Dispose(bool isDisposing)
        {
            if (this.device != null)
            {
                this.device.Dispose();
                this.device = null;
            }
        }

        /// <summary>
        /// Re-initialize the device.
        /// </summary>
        public async Task<bool> ResetConnection()
        {
            return await this.device.Initialize();
        }

        /// <summary>
        /// Query the PCM's VIN.
        /// </summary>
        public async Task<Response<string>> QueryVin()
        {
            if (!await this.device.SendMessage(this.messageFactory.CreateVinRequest1()))
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. Request for block 1 failed.");
            }

            Message response1 = await this.device.ReceiveMessage();
            if (response1 == null)
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. No response to request for block 1.");
            }

            if (!await this.device.SendMessage(this.messageFactory.CreateVinRequest2()))
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. Request for block 2 failed.");
            }

            Message response2 = await this.device.ReceiveMessage();
            if (response2 == null)
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. No response to request for block 2.");
            }

            if (!await this.device.SendMessage(this.messageFactory.CreateVinRequest3()))
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. Request for block 3 failed.");
            }

            Message response3 = await this.device.ReceiveMessage();
            if (response3 == null)
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. No response to request for block 3.");
            }

            return this.messageParser.ParseVinResponses(response1.GetBytes(), response2.GetBytes(), response3.GetBytes());
        }

        /// <summary>
        /// Query the PCM's Serial Number.
        /// </summary>
        public async Task<Response<string>> QuerySerial()
        {
            if (!await this.device.SendMessage(this.messageFactory.CreateSerialRequest1()))
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. Request for block 1 failed.");
            }

            Message response1 = await this.device.ReceiveMessage();
            if (response1 == null)
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. No response to request for block 1.");
            }

            if (!await this.device.SendMessage(this.messageFactory.CreateSerialRequest2()))
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. Request for block 2 failed.");
            }

            Message response2 = await this.device.ReceiveMessage();
            if (response2 == null)
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. No response to request for block 2.");
            }

            if (!await this.device.SendMessage(this.messageFactory.CreateSerialRequest3()))
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. Request for block 3 failed.");
            }

            Message response3 = await this.device.ReceiveMessage();
            if (response3 == null)
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. No response to request for block 3.");
            }

            return this.messageParser.ParseSerialResponses(response1, response2, response3);
        }

        /// <summary>
        /// Query the PCM's Broad Cast Code.
        /// </summary>
        public async Task<Response<string>> QueryBCC()
        {
            if (!await this.device.SendMessage(this.messageFactory.CreateBCCRequest()))
            {
                return Response.Create(ResponseStatus.Error, "Unknown. Request failed.");
            }

            Message response = await this.device.ReceiveMessage();
            if (response == null)
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. No response received.");
            }

            return this.messageParser.ParseBCCresponse(response.GetBytes());
        }

        /// <summary>
        /// Query the PCM's Manufacturer Enable Counter (MEC)
        /// </summary>
        public async Task<Response<string>> QueryMEC()
        {
            if (!await this.device.SendMessage(this.messageFactory.CreateMECRequest()))
            {
                return Response.Create(ResponseStatus.Error, "Unknow. Request failed.");
            }

            Message response = await this.device.ReceiveMessage();
            if (response == null)
            {
                return Response.Create(ResponseStatus.Timeout, "Unknown. No response received.");
            }

            return this.messageParser.ParseMECresponse(response.GetBytes());
        }

        /// <summary>
        /// Update the PCM's VIN
        /// </summary>
        /// <remarks>
        /// Requires that the PCM is already unlocked
        /// </remarks>
        public async Task<Response<bool>> UpdateVin(string vin)
        {
            if (vin.Length != 17) // should never happen, but....
            {
                this.logger.AddUserMessage("VIN " + vin + " is not 17 characters long!");
                return Response.Create(ResponseStatus.Error, false);
            }

            this.logger.AddUserMessage("Changing VIN to " + vin);

            byte[] bvin = Encoding.ASCII.GetBytes(vin);
            byte[] vin1 = new byte[6] { 0x00, bvin[0], bvin[1], bvin[2], bvin[3], bvin[4] };
            byte[] vin2 = new byte[6] { bvin[5], bvin[6], bvin[7], bvin[8], bvin[9], bvin[10] };
            byte[] vin3 = new byte[6] { bvin[11], bvin[12], bvin[13], bvin[14], bvin[15], bvin[16] };

            this.logger.AddUserMessage("Block 1");
            Response<bool> block1 = await WriteBlock(BlockId.Vin1, vin1);
            if (block1.Status != ResponseStatus.Success) return Response.Create(ResponseStatus.Error, false);
            this.logger.AddUserMessage("Block 2");
            Response<bool> block2 = await WriteBlock(BlockId.Vin2, vin2);
            if (block2.Status != ResponseStatus.Success) return Response.Create(ResponseStatus.Error, false);
            this.logger.AddUserMessage("Block 3");
            Response<bool> block3 = await WriteBlock(BlockId.Vin3, vin3);
            if (block3.Status != ResponseStatus.Success) return Response.Create(ResponseStatus.Error, false);

            return Response.Create(ResponseStatus.Success, true);
        }

        /// <summary>
        /// Query the PCM's operating system ID.
        /// </summary>
        /// <returns></returns>
        public async Task<Response<UInt32>> QueryOperatingSystemId()
        {
            Message request = this.messageFactory.CreateOperatingSystemIdReadRequest();
            return await this.QueryUnsignedValue(request);
        }

        /// <summary>
        /// Query the PCM's Hardware ID.
        /// </summary>
        /// <remarks>
        /// Note that this is a software variable and my not match the hardware at all of the software runs.
        /// </remarks>
        /// <returns></returns>
        public async Task<Response<UInt32>> QueryHardwareId()
        {
            Message request = this.messageFactory.CreateHardwareIdReadRequest();
            return await this.QueryUnsignedValue(request);
        }

        /// <summary>
        /// Query the PCM's Hardware ID.
        /// </summary>
        /// <remarks>
        /// Note that this is a software variable and my not match the hardware at all of the software runs.
        /// </remarks>
        /// <returns></returns>
        public async Task<Response<UInt32>> QueryCalibrationId()
        {
            Message request = this.messageFactory.CreateCalibrationIdReadRequest();
            return await this.QueryUnsignedValue(request);
        }

        private async Task<Response<UInt32>> QueryUnsignedValue(Message request)
        {
            if (!await this.device.SendMessage(request))
            {
                return Response.Create(ResponseStatus.Error, (UInt32)0);
            }

            var response = await this.device.ReceiveMessage();
            if (response == null)
            {
                return Response.Create(ResponseStatus.Timeout, (UInt32)0);
            }

            return this.messageParser.ParseBlockUInt32(response.GetBytes());
        }

        /// <summary>
        /// Send a 'test device present' notification.
        /// </summary>
        private async Task NotifyTestDevicePreset()
        {
            this.logger.AddDebugMessage("Sending 'test device present' notification.");
            Message testDevicePresent = this.messageFactory.CreateDevicePresentNotification();
            await this.device.SendMessage(testDevicePresent);
        }

        private async Task SuppressChatter()
        {
            this.logger.AddDebugMessage("Suppressing VPW chatter.");
            Message suppressChatter = this.messageFactory.CreateDisableNormalMessageTransmission();
            await this.device.SendMessage(suppressChatter);
        }

        /// <summary>
        /// Unlock the PCM by requesting a 'seed' and then sending the corresponding 'key' value.
        /// </summary>
        public async Task<Response<bool>> UnlockEcu(int keyAlgorithm)
        {
            /*
            await this.NotifyTestDevicePreset();

            Message seedRequest = this.messageFactory.CreateSeedRequest();
            Response<Message> seedResponse = await this.device.SendRequest(seedRequest);
            if (seedResponse.Status != ResponseStatus.Success)
            {
                if (seedResponse.Status != ResponseStatus.UnexpectedResponse) Response.Create(ResponseStatus.Success, true);
                return Response.Create(seedResponse.Status, false);
            }

            if (this.messageParser.IsUnlocked(seedResponse.Value.GetBytes()))
            {
                this.logger.AddUserMessage("PCM is already unlocked");
                return Response.Create(ResponseStatus.Success, true);
            }

            Response<UInt16> seedValueResponse = this.messageParser.ParseSeed(seedResponse.Value.GetBytes());
            if (seedValueResponse.Status != ResponseStatus.Success)
            {
                return Response.Create(seedValueResponse.Status, false);
            }

            if (seedValueResponse.Value == 0x0000)
            {
                this.logger.AddUserMessage("PCM Unlock not required");
                return Response.Create(seedValueResponse.Status, true);
            }

            UInt16 key = KeyAlgorithm.GetKey(keyAlgorithm, seedValueResponse.Value);

            Message unlockRequest = this.messageFactory.CreateUnlockRequest(key);
            Response<Message> unlockResponse = await this.device.SendRequest(unlockRequest);
            if (unlockResponse.Status != ResponseStatus.Success)
            {
                return Response.Create(unlockResponse.Status, false);
            }

            string errorMessage;
            Response<bool> result = this.messageParser.ParseUnlockResponse(unlockResponse.Value.GetBytes(), out errorMessage);
            if (errorMessage != null)
            {
                this.logger.AddUserMessage(errorMessage);
            }
            else
            {
                this.logger.AddUserMessage("PCM Unlocked");
            }
            
            return result;
            */
            return Response.Create(ResponseStatus.Error, false);
        }

        /// <summary>
        /// Writes a block of data to the PCM
        /// Requires an unlocked PCM
        /// </summary>
        private async Task<Response<bool>> WriteBlock(byte block, byte[] data)
        {
            /*
            Message tx;
            Message ok = new Message(new byte[] { 0x6C, DeviceId.Tool, DeviceId.Pcm, 0x7B, block });

            switch (data.Length)
            {
                case 6:
                    tx = new Message(new byte[] { 0x6C, DeviceId.Pcm, DeviceId.Tool, 0x3B, block, data[0], data[1], data[2], data[3], data[4], data[5] });
                    break;
                default:
                    logger.AddDebugMessage("Cant write block size " + data.Length);
                    return Response.Create(ResponseStatus.Error, false);
            }

            Response<Message> rx = await this.device.SendRequest(tx);

            if (rx.Status != ResponseStatus.Success)
            {
                logger.AddUserMessage("Failed to write block " + block + ", communications failure");
                return Response.Create(ResponseStatus.Error, false);
            }

            if (!Utility.CompareArrays(rx.Value.GetBytes(), ok.GetBytes()))
            {
                logger.AddUserMessage("Failed to write block " + block + ", PCM rejected attempt");
                return Response.Create(ResponseStatus.Error, false);
            }

            logger.AddDebugMessage("Successful write to block " + block);
            return Response.Create(ResponseStatus.Success, true);
            */
            return Response.Create(ResponseStatus.Error, false);
        }

        public async Task<byte[]> LoadKernelFromFidle(string kernel)
        {
            using (Stream stream = File.OpenRead(kernel))
            {
                byte[] contents = new byte[stream.Length];
                await stream.ReadAsync(contents, 0, (int)stream.Length);
                return contents;
            }
        }

        public async Task<Response<byte[]>> LoadKernelFromFile(string path)
        {
            byte[] file = { 0x00 }; // dummy value

            if (path == "") return Response.Create(ResponseStatus.Error, file);

            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string exeDirectory = Path.GetDirectoryName(exePath);
            path = Path.Combine(exeDirectory, path);

            try
            {
                using (Stream fileStream = File.OpenRead(path))
                {
                    file = new byte[fileStream.Length];

                    // In theory we might need a loop here. In practice, I don't think that will be necessary.
                    int bytesRead = await fileStream.ReadAsync(file, 0, (int)fileStream.Length);

                    if(bytesRead != fileStream.Length)
                    {
                        return Response.Create(ResponseStatus.Truncated, file);
                    }
                }
                
                logger.AddDebugMessage("Loaded " + path);
            }
            catch (ArgumentException)
            {
                logger.AddDebugMessage("Invalid file path " + path);
                return Response.Create(ResponseStatus.Error, file);
            }
            catch (PathTooLongException)
            {
                logger.AddDebugMessage("File path is too long " + path);
                return Response.Create(ResponseStatus.Error, file);
            }
            catch (DirectoryNotFoundException)
            {
                logger.AddDebugMessage("Invalid directory " + path);
                return Response.Create(ResponseStatus.Error, file);
            }
            catch (IOException)
            {
                logger.AddDebugMessage("Error accessing file " + path);
                return Response.Create(ResponseStatus.Error, file);
            }
            catch (UnauthorizedAccessException)
            {
                logger.AddDebugMessage("No permission to read file " + path);
                return Response.Create(ResponseStatus.Error, file);
            }

            return Response.Create(ResponseStatus.Success, file);
        }

        /// <summary>
        /// Read the full contents of the PCM.
        /// Assumes the PCM is unlocked an were ready to go
        /// </summary>
        public async Task<Response<Stream>> ReadContents(PcmInfo info)
        {
            /*
            try
            {
                // switch to 4x, if possible. But continue either way.
                // if the vehicle bus switches but the device does not, the bus will need to time out to revert back to 1x, and the next steps will fail.
                await this.VehicleSetVPW4x(true);

                // execute read kernel
                Response<byte[]> response = await LoadKernelFromFile("kernel.bin");
                if (response.Status != ResponseStatus.Success)
                {
                    logger.AddUserMessage("Failed to load kernel from file.");
                    return new Response<Stream>(response.Status, null);
                }

                // TODO: instead of this hard-coded 0xFF9150, get the base address from the PcmInfo object.
                if (!await PCMExecute(response.Value, 0xFF9150))
                {
                    logger.AddUserMessage("Failed to upload kernel uploaded to PCM");
                    return new Response<Stream>(ResponseStatus.Error, null);
                }

                logger.AddUserMessage("kernel uploaded to PCM succesfully");

                int startAddress = info.ImageBaseAddress;
                int endAddress = info.ImageBaseAddress + info.ImageSize;
                int bytesRemaining = info.ImageSize;
                int blockSize = 200; // Works with the ScanTool SX, will take about an  hour to download 512kb.

                byte[] image = new byte[info.ImageSize];

                while (startAddress < endAddress)
                {
                    await this.SuppressChatter();

                    if (startAddress + blockSize > endAddress)
                    {
                        blockSize = endAddress - startAddress;
                    }

                    if (blockSize < 1)
                    {
                        this.logger.AddUserMessage("Image download complete");
                        break;
                    }

//                     await this.NotifyTestDevicePreset();

                    if (!await TryReadBlock(image, startAddress, blockSize))
                    {
                        this.logger.AddUserMessage(
                            string.Format(
                                "Unable to read block from {0} to {1}",
                                startAddress,
                                blockSize));
                        return new Response<Stream>(ResponseStatus.Error, null);
                    }

                    startAddress += blockSize;
                }

                MemoryStream stream = new MemoryStream(image);
                return new Response<Stream>(ResponseStatus.Success, stream);
            }
            catch(Exception exception)
            {
                this.logger.AddUserMessage("Something went wrong. " + exception.Message);
                this.logger.AddDebugMessage(exception.ToString());
                return new Response<Stream>(ResponseStatus.Error, null);
            }
            finally
            {
                // Sending the exit command twice, and at both speeds, just to
                // be totally certain that the PCM goes back to normal. If the
                // kernel is left running, the engine won't start, and the 
                // dashboard lights up with all sorts of errors.
                //
                // You can reset by pulling the PCM's fuse, but I'd hate to 
                // have a think that we've done some real damage before they
                // figure that out.
                await this.ExitKernel();
                await this.VehicleSetVPW4x(false);
                await this.ExitKernel();
            }
            */

            return Response.Create(ResponseStatus.Error, (Stream)null);
        }

        public async Task ExitKernel()
        {
            Message exitKernel = this.messageFactory.CreateExitKernel();
            await this.device.SendMessage(exitKernel);
        }

        private async Task<bool> TryReadBlock(byte[] image, int startAddress, int length)
        {
            /*
            this.logger.AddDebugMessage(string.Format("Reading from {0}, length {1}", startAddress, length));
            
            for(int attempt = 1; attempt <= 5; attempt++)
            {
                Message message = this.messageFactory.CreateReadRequest(startAddress, length);

                this.logger.AddDebugMessage("Sending " + message.GetBytes().ToHex());
                Response<Message> response = await this.device.SendRequest(message);

                if(response.Status != ResponseStatus.Success)
                {
                    this.logger.AddUserMessage("Unable to send:" + response.Status);
                    continue;
                }

                this.logger.AddDebugMessage("Received " + response.Value.GetBytes().ToHex());

                if (this.messageParser.ParseReadResponse(response.Value.GetBytes()).Value)
                {
                    // this.logger.AddUserMessage("Read request succeeded, waiting for data.");

                    Response<Message> payloadResponse = await this.device.ReadMessage();
                    if (payloadResponse.Status != ResponseStatus.Success)
                    {
                        this.logger.AddUserMessage("Error receiving payload: " + payloadResponse.Status.ToString());
                        continue;
                    }

                    int percentDone = (startAddress * 100) / image.Length;
                    this.logger.AddUserMessage(string.Format("Read block starting at {0} / 0x{0:X}. {1}%", startAddress, percentDone));

                    Message payloadMessage = payloadResponse.Value;
                    byte[] payload = payloadMessage.GetBytes();
                    
                    if (payload.Length < 4)
                    {
                        this.logger.AddUserMessage("Payload too small, " + payload.Length.ToString() + " bytes.");
                        continue;
                    }

                    if (payload[4] == 1) // TODO check length
                    {
                        Buffer.BlockCopy(payload, 10, image, startAddress, length);
                    }
                    else if (payload[4] == 2) // TODO check length
                    {
                        int runLength = payload[5] << 8 + payload[6];
                        byte value = payload[10];
                        for (int index = 0; index < runLength; index++)
                        {
                            image[startAddress + index] = value;
                        }
                    }

                    return true;
                }                
            }

            return false;
            */
            return false;
        }

        /// <summary>
        /// Replace the full contents of the PCM.
        /// </summary>
        public Task<bool> WriteContents(Stream stream)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Load the executable payload on the PCM at the supplied address, and execute it.
        /// </summary>
        public async Task<bool> PCMExecute(byte[] payload, int address)
        {
            await this.SuppressChatter();

            logger.AddDebugMessage("Calling CreateBlockMessage with payload size " + payload.Length + ", loadaddress " + address.ToString("X6"));
            Message request = messageFactory.CreateUploadRequest(payload.Length, address);
            Response<Message> response = await SendRequest(request, 5);
            if (response.Status != ResponseStatus.Success)
            {
                logger.AddDebugMessage("Could not upload kernel to PCM, permission denied.");
                return false;
            }

            logger.AddDebugMessage("Going to load a " + payload.Length + " byte payload to 0x" + address.ToString("X6"));
            // Loop through the payload building and sending packets, highest first, execute on last

            int payloadSize = device.MaxSendSize - 12; // Headers use 10 bytes, sum uses 2 bytes.
            int chunkCount = payload.Length / payloadSize;
            int remainder = payload.Length % payloadSize;

            int offset = (chunkCount * payloadSize);
            int startAddress = address + offset;
            logger.AddDebugMessage(
                string.Format(
                    "Sending remainder payload with offset 0x{0:X}, start address 0x{1:X}, length 0x{2:X}.",
                    offset,
                    startAddress,
                    remainder));

            Message remainderMessage = messageFactory.CreateBlockMessage(
                payload, 
                offset, 
                remainder, 
                address + offset, 
                remainder == payload.Length);

            response = await SendRequest(remainderMessage, 5);
            if (response.Status != ResponseStatus.Success)
            {
                logger.AddDebugMessage("Could not upload kernel to PCM, remainder payload not accepted.");
                return false;
            }

            for (int chunkIndex = chunkCount; chunkIndex > 0; chunkIndex--)
            {
                await this.SuppressChatter();

                offset = (chunkIndex - 1) * payloadSize;
                startAddress = address + offset;
                Message payloadMessage = messageFactory.CreateBlockMessage(
                    payload,
                    offset,
                    payloadSize,
                    startAddress,
                    offset == 0);

                logger.AddDebugMessage(
                    string.Format(
                        "Sending payload with offset 0x{0:X}, start address 0x{1:X}, length 0x{2:X}.",
                        offset,
                        startAddress,
                        payloadSize));

                response = await SendRequest(payloadMessage, 5);
                if (response.Status != ResponseStatus.Success)
                {
                    logger.AddDebugMessage("Could not upload kernel to PCM, payload not accepted.");
                    return false;
                }

                int bytesSent = payload.Length - offset;
                int percentDone = bytesSent * 100 / payload.Length;

                this.logger.AddUserMessage(
                    string.Format(
                        "Kernel upload {0}% complete.",
                        percentDone));
            }

            return true;
        }

        /// <summary>
        /// Does everything required to switch to VPW 4x
        /// </summary>
        public async Task<bool> VehicleSetVPW4x(bool highspeed)
        {
            Message HighSpeedCheck = messageFactory.CreateHighSpeedCheck();
            Message HighSpeedOK = messageFactory.CreateHighSpeedOKResponse();
            Message BeginHighSpeed = messageFactory.CreateBeginHighSpeed();

            if (!device.Supports4X)
            {
                logger.AddUserMessage("This interface does not support VPW 4x");
                return true;
            }

            logger.AddUserMessage("This interface does support VPW 4x");

            // PCM Pre-flight checks
            if (!await this.device.SendMessage(HighSpeedCheck))
            {
                logger.AddUserMessage("Unable to request permission to use 4x.");
                return false;
            }

            Message rx = await this.device.ReceiveMessage();
            if (rx == null)
            {
                logger.AddUserMessage("No response received to high-speed permission request.");
                return false;
            }

            if (!Utility.CompareArraysPart(rx.GetBytes(), HighSpeedOK.GetBytes()))
            {
                logger.AddUserMessage("PCM is not allowing a switch to VPW 4x");
                return false;
            }

            logger.AddUserMessage("PCM is allowing a switch to VPW 4x. Requesting all VPW modules to do so.");
            if(!await this.device.SendMessage(BeginHighSpeed))
            {
                return false;
            }

            rx = await this.device.ReceiveMessage();

            // Request the device to change
            await device.SetVPW4x(true);

            return true;
        }

        /// <summary>
        /// Sends the provided message retries times, with a small delay on fail. 
        /// </summary>
        /// <remarks>
        /// Returns a succsefull Response on the first successful attempt, or the failed Response if we run out of tries.
        /// </remarks>
        async Task<Response<Message>> SendRequest(Message message, int retries)
        {
            for (int i = retries; i>0; i--)
            {
                if (!await device.SendMessage(message))
                {
                    this.logger.AddDebugMessage("Unable to send message.");
                    continue;
                }

                Message response = await this.device.ReceiveMessage();
                if (response != null)
                {
                    return Response.Create(ResponseStatus.Success, response);
                }

                await Task.Delay(10); // incase were going too fast, we might want to change this logic
            }

            return Response.Create(ResponseStatus.Error, (Message)null); // this should be response from the loop but the compiler thinks the response variable isnt in scope here????
        }
    }
}
