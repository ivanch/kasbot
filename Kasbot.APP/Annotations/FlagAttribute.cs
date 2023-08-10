namespace Kasbot.Annotations
{
    public class FlagAttribute : Attribute
    {
        public List<string> Names { get; set; }

        public FlagAttribute() { }

        public FlagAttribute(params string[] names)
        {
            Names = names.ToList();
        }
    }
}
