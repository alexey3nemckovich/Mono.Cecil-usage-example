namespace Mono_logger
{

    class Program
    {

        static void Main(string[] args)
        {
            MonoLogger monoLogger = new MonoLogger();
            monoLogger.InjectCodeIntoAssembly(args[0]);
        }

    }

}