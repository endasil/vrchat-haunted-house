namespace Assets._3DStealthGame.Scripts
{
    public static class KeyTypeHelper
    {
        public static string GetName(KeyType keyType)
        {
            switch (keyType)
            {
                case KeyType.Green: return "Green";
                case KeyType.Red: return "Red";
                case KeyType.Blue: return "Blue";
                case KeyType.Brown: return "Brown";
                default: return "Unknown";
            }
        }
    }
}