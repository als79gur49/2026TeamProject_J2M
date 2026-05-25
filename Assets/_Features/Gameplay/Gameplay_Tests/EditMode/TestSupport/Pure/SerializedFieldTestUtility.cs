using System;
using System.Reflection;

namespace Game.Feature.Gameplay.Tests.Support.Pure
{
    public static class SerializedFieldTestUtility
    {
        public static T WithSerializedField<T>(T instance, string fieldName, object value)
        {
            var boxed = (object)instance;
            SetSerializedField(boxed, typeof(T), fieldName, value);
            return (T)boxed;
        }

        public static void SetSerializedField(object instance, string fieldName, object value)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            SetSerializedField(instance, instance.GetType(), fieldName, value);
        }

        private static void SetSerializedField(object instance, Type declaringType, string fieldName, object value)
        {
            // Tests use this to inject legacy private serialized data without exposing compatibility fields in runtime APIs.
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(declaringType.FullName, fieldName);
            }

            field.SetValue(instance, value);
        }
    }
}
