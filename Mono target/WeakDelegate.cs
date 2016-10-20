using System;
using System.Linq.Expressions;
using System.Reflection;

namespace WeakDelegate
{

    public class WeakDelegate
    {

        private Type eventHandlerDelegateType;
        private MethodInfo targetEventHandlerMethodInfo;
        public WeakReference weakReferenceToTarget;
        private Delegate proxyDelegate;

        public WeakDelegate(Delegate eventHandler)
        {
            eventHandlerDelegateType = eventHandler.GetType();
            targetEventHandlerMethodInfo = eventHandler.Method;
            weakReferenceToTarget = new WeakReference(eventHandler.Target);
            InitProxyDelegate();
        }
        
        private void InitProxyDelegate()
        {
            //parameters expression
            ParameterExpression[] eventHandlerArgsExpressionMassive = GetParametersExpression(targetEventHandlerMethodInfo);
            //target property expression
            Expression weakReferenceExpression = Expression.Constant(weakReferenceToTarget);
            Type typeToCastProperty = weakReferenceToTarget.Target.GetType();
            Expression targetObjectExpression = GetPropertyExpression(weakReferenceExpression, "Target", typeToCastProperty);
            //call of target event handler expression
            Expression targetMethodInvoke = Expression.Call(targetObjectExpression, targetEventHandlerMethodInfo, eventHandlerArgsExpressionMassive);
            //if expression
            Expression nullExpression = Expression.Constant(null);
            Expression conditionExpression = Expression.NotEqual(targetObjectExpression, nullExpression);
            Expression ifExpression = Expression.IfThen(conditionExpression, targetMethodInvoke);
            //compiling expression to code
            LambdaExpression labmda = Expression.Lambda(ifExpression, eventHandlerArgsExpressionMassive);
            proxyDelegate = labmda.Compile();
        }

        private ParameterExpression[] GetParametersExpression(MethodInfo method)
        {
            ParameterInfo[] eventHandlerArgsInfo = method.GetParameters();
            ParameterExpression[] eventHandlerArgsExpressionMassive = new ParameterExpression[eventHandlerArgsInfo.Length];
            for (int i = 0; i < eventHandlerArgsInfo.Length; i++)
            {
                eventHandlerArgsExpressionMassive[i] = ParameterExpression.Parameter(eventHandlerArgsInfo[i].ParameterType);
            }
            return eventHandlerArgsExpressionMassive;
        }

        private Expression GetPropertyExpression(Expression declaryingObjectExpression, String propertyName, Type typeToCastProperty = null)
        {
            Type declaryingClassType = declaryingObjectExpression.Type;
            PropertyInfo targetPropertyInfo = declaryingClassType.GetProperty(propertyName);
            Expression targetObjectExpression = Expression.Property(declaryingObjectExpression, targetPropertyInfo);
            if(typeToCastProperty != null)
            {
                targetObjectExpression = Expression.Convert(targetObjectExpression, typeToCastProperty);
            }
            return targetObjectExpression;
        }

        public Delegate Weak
        {
            get
            {
                return proxyDelegate;
            }
        }

    }

}