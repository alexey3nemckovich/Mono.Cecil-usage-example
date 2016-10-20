namespace Mono_target
{

    public class A
    {

        public A(int x)
        {

        }

    }

    [Log]
    public class TargetAopClass : A
    {

        public TargetAopClass() : base(3)
        {

        }

        public TargetAopClass(int parameter) : base(parameter)
        {

        }

        public void First()
        {
            int x = PrivateMethod(5);//res 25
            x = SecondPrivateMethod(x);//res 35
        }

        public void Second(int x)
        {

        }

        public int Third(int a, int b)
        {
            return a + b;
        }

        private int PrivateMethod(int x)
        {
            return x * x;
        }

        private int SecondPrivateMethod(int x)
        {
            return x + 10;
        }

        private static int PrivateStatMethod()
        {
            return 5;
        }

        public static int StatMethodThatCallsStatMethod(int x)
        {
            return x + PrivateStatMethod();
        }

        public int MethodWithFewParameters(int x, object y)
        {
            return x - 3;
        }

        public void MethodWithRefParameters(ref short x, ref object y)
        {
            x -= 3;
            y = x.ToString();
        }

        public void MethodWithOutParameters(out byte x, out object y)
        {
            x = 7;
            y = x.ToString();
        }

    }

}