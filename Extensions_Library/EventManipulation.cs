using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Extensions_Library
{
    public static class EventManipulation
    {
        private static FieldInfo GetEventField(this Type type, string eventName)
        {
            FieldInfo field = null;

            while (type != null)
            {
                /* Find events defined as field */
                field = type.GetField(eventName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && (field.FieldType == typeof(MulticastDelegate) || field.FieldType.IsSubclassOf(typeof(MulticastDelegate))))
                    break;

                /* Find events defined as property { add; remove; } */
                field = type.GetField("_"+ eventName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    break;
                type = type.BaseType;
            }

            return field;
        }

        public static MulticastDelegate GetDelegates<T>(this T obj, string eventName)
        {
            FieldInfo eventfield = GetEventField(obj.GetType(), eventName);
            if (eventfield == null) return null;

            MulticastDelegate returnDelegate = eventfield.GetValue(obj) as MulticastDelegate;

            return returnDelegate;
        }

        public static void SetDelegates<T>(this T obj, string eventName, MulticastDelegate delegates)
        {
            FieldInfo eventfield = GetEventField(obj.GetType(), eventName);
            if (eventfield == null) return;

            eventfield.SetValue(obj, delegates);
        }
    }
}
