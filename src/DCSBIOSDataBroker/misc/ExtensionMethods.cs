namespace DCSBIOSDataBroker.misc
{
    internal static class ExtensionMethods
    {

        public static string DecodeException(this Exception e)
        {
            if (e == null)
            {
                return null;
            }
            return e.Message + "\n" + e.StackTrace;
        }
    }
}
