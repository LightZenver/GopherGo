using System;
using System.Collections.Generic;
using Burrow.Runtime;

class Program
{
    static void Main(string[] args)
    {
        var program = new List<Instruction>
        {
            new Instruction(OpCode.PushInt, 100),
            new Instruction(OpCode.PushInt, 0),
            new Instruction(OpCode.Divide),
            new Instruction(OpCode.Print),
            new Instruction(OpCode.Halt)
        };

        var vm = new VirtualMachine(Console.Out);
        vm.Execute(program);
        Console.WriteLine("GVM Completed!");
    }   
}
