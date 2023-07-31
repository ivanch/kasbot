using Kasbot.Annotations;

namespace Kasbot.Models
{
    public class Flags
    {
        [Flag("-s", "-silent")]
        public bool Silent { get; set; }

        [Flag("-r", "-repeat")]
        public bool Repeat { get; set; }

        public Flags() { }

        public Flags(string command)
        {
            this.Parse(command);
        }

        public string Parse(string command)
        {
            string result = command;

            this.GetType().GetProperties().ToList().ForEach(prop =>
            {
                Attribute.GetCustomAttributes(prop).ToList().ForEach(attr =>
                {
                    if (attr is FlagAttribute flag)
                    {
                        flag.Names.ForEach(name =>
                        {
                            if (command.Contains(name))
                            {
                                prop.SetValue(this, true);
                                result.Replace(name, string.Empty);
                            }
                        });
                    }
                });
            });

            return result;
        }
    }
}
