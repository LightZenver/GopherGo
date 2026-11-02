using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Burrow.Runtime
{
    public class GvmParser
    {
        public IList<Instruction> ParseFile(string path)
        {
            return Parse(File.ReadAllLines(path));
        }

        public IList<Instruction> Parse(IEnumerable<string> lines)
        {
            var program = new List<Instruction>();
            var addresses = new HashSet<int>();

            int lineNo = 0;
            foreach (string rawLine in lines)
            {
                lineNo++;

                string line = StripComment(rawLine).Trim();
                if (line.Length == 0)
                    continue;

                int colon = line.IndexOf(':');
                if (colon <= 0)
                    throw new GvmParseException(lineNo, rawLine, "Expected format '<address>: <opcode> [operand]'");

                string addrPart = line.Substring(0, colon).Trim();
                string body = line.Substring(colon + 1).Trim();

                if (!int.TryParse(addrPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int address))
                    throw new GvmParseException(lineNo, rawLine, $"Invalid address '{addrPart}'");

                if (!addresses.Add(address))
                    throw new GvmParseException(lineNo, rawLine, $"Duplicate address {address}");

                string[] tokens = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    throw new GvmParseException(lineNo, rawLine, "Empty instruction");

                string opName = tokens[0];
                if (!Enum.TryParse<OpCode>(opName, ignoreCase: true, out OpCode opcode))
                    throw new GvmParseException(lineNo, rawLine, $"Unknown opcode '{opName}'");

                string operandStr = tokens.Length > 1 ? tokens[1] : null;
                if (tokens.Length > 2)
                    throw new GvmParseException(lineNo, rawLine, "Too many tokens");

                Instruction instruction = CreateInstruction(opcode, operandStr, lineNo, rawLine);
                instruction.Address = address;
                program.Add(instruction);
            }

            return program;
        }

        private static Instruction CreateInstruction(OpCode opcode, string operandStr, int lineNo, string rawLine)
        {
            switch (opcode)
            {
                case OpCode.PushInt:
                    if (operandStr == null)
                        throw new GvmParseException(lineNo, rawLine, "PushInt requires int operand");
                    if (!int.TryParse(operandStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intVal))
                        throw new GvmParseException(lineNo, rawLine, $"Invalid int '{operandStr}'");
                    return new Instruction(opcode, intVal);

                case OpCode.PushBool:
                    if (operandStr == null)
                        throw new GvmParseException(lineNo, rawLine, "PushBool requires bool operand");
                    if (!bool.TryParse(operandStr, out bool boolVal))
                        throw new GvmParseException(lineNo, rawLine, $"Invalid bool '{operandStr}'");
                    return new Instruction(opcode, boolVal);

                default:
                    if (operandStr != null)
                        throw new GvmParseException(lineNo, rawLine, $"Opcode {opcode} does not take an operand");
                    return new Instruction(opcode);
            }
        }

        private static string StripComment(string line)
        {
            int idx = line.IndexOf(';');
            return idx >= 0 ? line.Substring(0, idx) : line;
        }
    }

    public class GvmParseException : Exception
    {
        public GvmParseException(int lineNo, string line, string message)
            : base($"Line {lineNo}: {message} (\"{line}\")")
        {
        }
    }
}