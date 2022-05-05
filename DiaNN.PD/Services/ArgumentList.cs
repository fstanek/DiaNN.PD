using System.Collections.Generic;

namespace DiaNN.PD.Services
{
    public class ArgumentList
    {
        private readonly List<string> arguments = new List<string>();

        public void Add(string name)
        {
            arguments.Add($"--{name}");
        }

        public void Add(string name, string value)
        {
            Add(name);

            if (value.Contains(" "))
                value = $"\"{value}\"";

            arguments.Add(value);
        }

        public void Add(string name, object value)
        {
            Add(name, value.ToString());
        }

        public override string ToString()
        {
            return string.Join(" ", arguments);
        }
    }
}