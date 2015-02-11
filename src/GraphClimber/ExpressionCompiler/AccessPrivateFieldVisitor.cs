using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace GraphClimber.ExpressionCompiler
{
    public class AccessPrivateFieldVisitor : ExpressionVisitor
    {
        public static readonly ExpressionVisitor Empty = new AccessPrivateFieldVisitor();

        /// <summary>
        /// Gets the field value from the given instance
        /// </summary>
        /// <param name="assemblyQualifiedName"></param>
        /// <param name="fieldName"></param>
        /// <param name="instance">The instance to get the field value from, Or null for static fields</param>
        /// <returns></returns>
        [DebuggerNonUserCode]
        public static object GetFieldValue(string assemblyQualifiedName, string fieldName, object instance)
        {
            return GetType(assemblyQualifiedName).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).GetValue(instance);
        }

        /// <summary>
        /// Gets the property value from the given instance
        /// </summary>
        /// <param name="assemblyQualifiedName"></param>
        /// <param name="propertyName"></param>
        /// <param name="instance">The instance to get the property value from, Or null for static properties</param>
        /// <returns></returns>
        [DebuggerNonUserCode]
        public static object GetPropertyValue(string assemblyQualifiedName, string propertyName, object instance)
        {
            return GetType(assemblyQualifiedName).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).GetValue(instance);
        }

        /// <summary>
        /// Calls the method on the given instance and returns the value from the method
        /// </summary>
        /// <param name="assemblyQualifiedName"></param>
        /// <param name="methodName"></param>
        /// <param name="genericArgumentTypes"></param>
        /// <param name="instance">The instance to call the method on, Or null for static methods</param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [DebuggerNonUserCode]
        public static object CallMethod(string assemblyQualifiedName, string methodName, string[] genericArgumentTypes,
            object instance,
            params object[] parameters)
        {
            var methodInfo = GetType(assemblyQualifiedName).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
           
            if (methodInfo.ContainsGenericParameters)
            {
                methodInfo = methodInfo.MakeGenericMethod(genericArgumentTypes.Select(GetType).ToArray());
            }

            return methodInfo.Invoke(instance, parameters);
        }

        /// <summary>
        /// Gets the given type or throws exception in case 
        /// it's not found.
        /// </summary>
        /// <param name="assemblyQualifiedName"></param>
        /// <returns></returns>
        private static Type GetType(string assemblyQualifiedName)
        {
            var returnValue = Type.GetType(assemblyQualifiedName);

            if (returnValue == null)
            {
                throw new NullReferenceException(
                    string.Format("Could not load type with assembly qualified name : '{0}', Exception thrown by '{1}'.",
                        assemblyQualifiedName, typeof (AccessPrivateFieldVisitor)));
            }

            return returnValue;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (!node.Method.IsPublic)
            {
                // In case of static methods, instance should be null.
                var reflectionInstance = node.Object ?? Expression.Constant(null);

                // In case of generic method, 
                // we create an array of the assembly qualified names of the generic arguments.
                var genericArgumentTypes = node.Method.IsGenericMethod 
                    ? (Expression)Expression.NewArrayInit(typeof(string), node.Method.GetGenericArguments().Select(t => Expression.Constant(t.AssemblyQualifiedName))) 
                    : Expression.Constant(null);

                return
                    Expression.Convert(Expression.Call(null, typeof(AccessPrivateFieldVisitor).GetMethod("CallMethod"),
                        Expression.Constant(node.Method.DeclaringType.AssemblyQualifiedName),
                        Expression.Constant(node.Method.Name),
                        genericArgumentTypes,
                        reflectionInstance,
                        Expression.NewArrayInit(typeof(object), node.Arguments)), node.Type);
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            var fieldInfo = node.Member as FieldInfo;
            if (fieldInfo != null)
            {
                return VisitFieldMemberExpression(node, fieldInfo);
            }

            var propertyInfo = node.Member as PropertyInfo;
            if (propertyInfo != null)
            {
                return VisitPropertyMemberExpression(node, propertyInfo);
            }


            return base.VisitMember(node);
        }

        private static Expression VisitFieldMemberExpression(MemberExpression node, FieldInfo fieldInfo)
        {
            FieldAttributes fieldAttributes = fieldInfo.Attributes;

            bool isPrivateField = fieldAttributes.HasFlag(FieldAttributes.Private) ||
                                  fieldAttributes.HasFlag(FieldAttributes.PrivateScope);

            if (isPrivateField)
            {
                var argument = node.Expression ?? Expression.Constant(null);

                return Expression.Convert(Expression.Call(null,
                    typeof (AccessPrivateFieldVisitor).GetMethod("GetFieldValue"),
                    Expression.Constant(fieldInfo.DeclaringType.AssemblyQualifiedName),
                    Expression.Constant(fieldInfo.Name), argument), node.Type);
            }

            return node;
        }

        private static Expression VisitPropertyMemberExpression(MemberExpression node, PropertyInfo propertyInfo)
        {
            bool isPrivate =
                new[] {propertyInfo.GetGetMethod(true), propertyInfo.GetSetMethod(true)}.Where(m => m != null)
                    .Select(m => m.Attributes)
                    .Any(m => m.HasFlag(MethodAttributes.Private) || m.HasFlag(MethodAttributes.PrivateScope));

            if (isPrivate)
            {
                var argument = node.Expression ?? Expression.Constant(null);

                return Expression.Convert(Expression.Call(null,
                    typeof (AccessPrivateFieldVisitor).GetMethod("GetPropertyValue"),
                    Expression.Constant(propertyInfo.DeclaringType.AssemblyQualifiedName),
                    Expression.Constant(propertyInfo.Name), argument), node.Type);
            }
            
            return node;
        }
    }
}