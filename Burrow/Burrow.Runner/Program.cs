using System;
using Burrow.Runtime;

class Program
{
    static void Main(string[] args)
    {
        var parser = new GvmParser();
        var program = parser.ParseFile("program.gvm");

        var vm = new VirtualMachine(Console.Out);
        vm.Execute(program);
    }
}