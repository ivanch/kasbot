namespace Kasbot.Models
{
    public class Flags
    {
        public bool Silent { get; set; }
        public bool Repeat { get; set; }

        public Flags() { }

        public Flags(string command)
        {
            this.Parse(command);
        }

        public void Parse(string command)
        {
            if (command.Contains("-s") ||
                command.Contains("-silent"))
            {
                Silent = true;
            }

            if (command.Contains("-r") ||
                command.Contains("-repeat"))
            {
                Repeat = true;
            }
        }
    }
}
