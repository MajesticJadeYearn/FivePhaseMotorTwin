using System;
using System.Collections.Generic;
using System.Globalization;

namespace FivePhaseMotorTwin
{
    public static class SerialTelemetryParser
    {
        private static readonly char[] TokenSeparators = new char[] { ',', ';', '\t', ' ' };

        public static bool TryParse(string line, out SignalSnapshot sample)
        {
            sample = null;
            if (string.IsNullOrWhiteSpace(line)) return false;

            line = line.Trim();
            if (line.StartsWith("#")) return false;

            Dictionary<string, double> values = ParseKeyValues(line);
            if (values.Count > 0)
            {
                SignalSnapshot keyed = new SignalSnapshot();
                keyed.Time = Get(values, "t", Get(values, "time", double.NaN));
                keyed.Ia = Get(values, "ia", 0.0);
                keyed.Ib = Get(values, "ib", 0.0);
                keyed.Ic = Get(values, "ic", 0.0);
                keyed.Id = Get(values, "id", 0.0);
                keyed.Ie = Get(values, "ie", 0.0);
                keyed.IaRef = Get(values, "ia_ref", Get(values, "iaref", 0.0));
                keyed.Speed = Get(values, "speed", Get(values, "rpm", 0.0));
                keyed.Torque = Get(values, "torque", 0.0);
                keyed.Iq = Get(values, "iq", 0.0);
                keyed.Residual = Get(values, "residual", 0.0);
                keyed.FaultFlag = Get(values, "fault_flag", Get(values, "flag", Get(values, "fault", 0.0)));
                sample = keyed;
                return true;
            }

            List<double> numeric = ParseNumbers(line);
            if (numeric.Count < 3) return false;

            SignalSnapshot csv = new SignalSnapshot();
            csv.Time = double.NaN;

            if (numeric.Count == 3)
            {
                ApplyCurrents(csv, numeric, 0, 3);
            }
            else if (numeric.Count == 4)
            {
                csv.Time = numeric[0];
                ApplyCurrents(csv, numeric, 1, 3);
            }
            else if (numeric.Count == 5)
            {
                ApplyCurrents(csv, numeric, 0, 5);
            }
            else if (numeric.Count == 6)
            {
                ApplyCurrents(csv, numeric, 0, 3);
                csv.Speed = numeric[3];
                csv.Torque = numeric[4];
                csv.Iq = numeric[5];
            }
            else if (numeric.Count == 8)
            {
                ApplyCurrents(csv, numeric, 0, 5);
                csv.Speed = numeric[5];
                csv.Torque = numeric[6];
                csv.Iq = numeric[7];
            }
            else if (numeric.Count == 9)
            {
                csv.Time = numeric[0];
                ApplyCurrents(csv, numeric, 1, 5);
                csv.Speed = numeric[6];
                csv.Torque = numeric[7];
                csv.Iq = numeric[8];
            }
            else if (numeric.Count == 10)
            {
                ApplyCurrents(csv, numeric, 0, 5);
                csv.Speed = numeric[5];
                csv.Torque = numeric[6];
                csv.Iq = numeric[7];
                csv.Residual = numeric[8];
                csv.FaultFlag = numeric[9];
            }
            else
            {
                csv.Time = numeric[0];
                ApplyCurrents(csv, numeric, 1, 5);
                csv.Speed = numeric[6];
                csv.Torque = numeric[7];
                csv.Iq = numeric[8];
                csv.Residual = numeric[9];
                csv.FaultFlag = numeric[10];
            }

            sample = csv;
            return true;
        }

        private static void ApplyCurrents(SignalSnapshot sample, List<double> values, int offset, int count)
        {
            sample.Ia = GetAt(values, offset, 0.0);
            sample.Ib = GetAt(values, offset + 1, 0.0);
            sample.Ic = GetAt(values, offset + 2, 0.0);
            if (count >= 5)
            {
                sample.Id = GetAt(values, offset + 3, 0.0);
                sample.Ie = GetAt(values, offset + 4, 0.0);
            }
        }

        private static Dictionary<string, double> ParseKeyValues(string line)
        {
            Dictionary<string, double> values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            string[] tokens = line.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                int index = token.IndexOf('=');
                if (index < 0) index = token.IndexOf(':');
                if (index <= 0 || index >= token.Length - 1) continue;

                string key = token.Substring(0, index).Trim();
                string valueText = token.Substring(index + 1).Trim();
                double value;
                if (TryParseDouble(valueText, out value))
                {
                    values[key] = value;
                }
            }
            return values;
        }

        private static List<double> ParseNumbers(string line)
        {
            List<double> values = new List<double>();
            string[] tokens = line.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                double value;
                if (!TryParseDouble(tokens[i], out value)) return new List<double>();
                values.Add(value);
            }
            return values;
        }

        private static bool TryParseDouble(string valueText, out double value)
        {
            return double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(valueText, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static double Get(Dictionary<string, double> values, string key, double defaultValue)
        {
            double value;
            return values.TryGetValue(key, out value) ? value : defaultValue;
        }

        private static double GetAt(List<double> values, int index, double defaultValue)
        {
            return index >= 0 && index < values.Count ? values[index] : defaultValue;
        }
    }
}
