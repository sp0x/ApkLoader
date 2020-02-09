namespace LibAppDeployer
{
    public class AndroidPackage
    {
        private string mName;
        public string Name => mName; 
        public AndroidPackage(string name)
        {
            mName = name;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}