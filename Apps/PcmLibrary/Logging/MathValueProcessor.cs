﻿using DynamicExpresso;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PcmHacking
{
    /// <summary>
    /// Combines a math column with the columns that it depends on.
    /// </summary>
    public class MathColumnAndDependencies
    {
        public LogColumn MathColumn { get; private set; }
        public LogColumn XColumn { get; private set; }
        public LogColumn YColumn { get; private set; }
        
        public MathColumnAndDependencies(
            LogColumn mathColumn,
            LogColumn xColumn,
            LogColumn yColumn)
        {
            this.MathColumn = mathColumn;
            this.XColumn = xColumn;
            this.YColumn = yColumn;
        }
    }

    /// <summary>
    /// Computes the values for math columns, based on data read from the PCM.
    /// </summary>
    public class MathValueProcessor
    {
        private readonly DpidConfiguration dpidConfiguration;
        private IEnumerable<MathColumnAndDependencies> mathColumns;

        /// <summary>
        /// Constructor
        /// </summary>
        public MathValueProcessor(DpidConfiguration dpidConfiguration, IEnumerable<MathColumnAndDependencies> mathColumns)
        {
            this.dpidConfiguration = dpidConfiguration;
            this.mathColumns = mathColumns;
        }

        /// <summary>
        /// Returns the names of the math columns.
        /// </summary>
        public IEnumerable<string> GetHeaderNames()
        {
            return this.mathColumns.Select(x => x.MathColumn.Parameter.Name);
        }

        /// <summary>
        /// Gets the math columns - the logger will concatenate these with the PCM columns.
        /// </summary>
        public IEnumerable<LogColumn> GetMathColumns()
        {
            return this.mathColumns.Select(x => x.MathColumn);
        }

        /// <summary>
        /// Get the values of the math columns as strings, suitable for display or writing to a log file.
        /// </summary>
        public IEnumerable<string> GetMathValues(PcmParameterValues dpidValues)
        {
            List<string> result = new List<string>();
            foreach(MathColumnAndDependencies value in this.mathColumns)
            {
                double xConverted = 0;
                double yConverted = 0;
                string error = null;

                try
                {
                    Int16 xParameterValue = dpidValues[value.XColumn].RawValue;
                    Interpreter xConverter = new Interpreter();
                    xConverter.SetVariable("x", xParameterValue);
                    xConverter.SetVariable("x_high", xParameterValue >> 8);
                    xConverter.SetVariable("x_low", xParameterValue & 0xFF);
                    xConverted = xConverter.Eval<double>(value.XColumn.Conversion.Expression);
                }
                catch (Exception exception)
                {
                    error = "X: " + exception.Message;
                    xConverted = 0;
                }

                try
                {
                    Int16 yParameterValue = dpidValues[value.YColumn].RawValue;
                    Interpreter yConverter = new Interpreter();
                    yConverter.SetVariable("x", yParameterValue);
                    yConverter.SetVariable("x_high", yParameterValue >> 8);
                    yConverter.SetVariable("x_low", yParameterValue & 0xFF);
                    yConverted = yConverter.Eval<double>(value.YColumn.Conversion.Expression);
                }
                catch (Exception exception)
                {
                    error = "Y: " + exception.Message;
                    yConverted = 0;
                }

                if (error != null)
                {
                    result.Add(error);
                }
                else
                {
                    try
                    {
                        Interpreter finalConverter = new Interpreter();
                        finalConverter.SetVariable("x", xConverted);
                        finalConverter.SetVariable("y", yConverted);
                        double converted = finalConverter.Eval<double>(value.MathColumn.Conversion.Expression);
                        result.Add(converted.ToString(value.MathColumn.Conversion.Format));
                    }
                    catch (Exception exception)
                    {
                        result.Add("Error: " + exception.Message);
                    }
                }
            }

            return result;
        }
    }
}

