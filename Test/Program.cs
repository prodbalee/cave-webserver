using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace Test
{
    class Program
    {
        static int Main(string[] args)
        {
            Mutex mutex;
            try
            {
                mutex = Mutex.OpenExisting(AppDomain.CurrentDomain.FriendlyName);
            }
            catch
            {
                mutex = null;
            }

            if (mutex == null)
            {
                mutex = new Mutex(false, AppDomain.CurrentDomain.FriendlyName);
            }
            else
            {
                mutex.Close();
                Console.WriteLine("Another test instance is already running!");
                return 0;
            }

            int errors = 0;
            try
            {
                Type[] types = typeof(Program).Assembly.GetTypes();
                foreach (Type type in types.OrderBy(t => t.Name))
                {
                    if (!type.GetCustomAttributes(typeof(TestFixtureAttribute), false).Any())
                    {
                        continue;
                    }

                    object instance = Activator.CreateInstance(type);
                    foreach (System.Reflection.MethodInfo method in type.GetMethods())
                    {
                        if (!method.GetCustomAttributes(typeof(TestAttribute), false).Any())
                        {
                            continue;
                        }

                        GC.Collect(999, GCCollectionMode.Default, true);

                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"{method.DeclaringType.Name}.cs: info TI0001: Start {method.Name}");
                        Console.ResetColor();
                        try
                        {
                            var action = (Action)method.CreateDelegate(typeof(Action), instance);
                            action();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"{method.DeclaringType.Name}.cs: info TI0002: Success {method.Name}");
                            Console.ResetColor();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"{method.DeclaringType.Name}.cs: error TE0001: {ex.Message}");
                            Console.WriteLine(ex);
                            Console.ResetColor();
                            errors++;
                        }
                        Console.WriteLine("---");
                    }
                }
                if (errors == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"---: info TI9999: All tests successfully completed.");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"---: error TE9999: {errors} tests failed!");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex);
                errors++;
            }
            finally
            {
                mutex.Close();
            }
            Console.ResetColor();
            if (Debugger.IsAttached)
            {
                WaitExit();
            }

            return errors;
        }

        static void WaitExit()
        {
            Console.Write("--- press enter to exit ---");
            while (Console.ReadKey(true).Key != ConsoleKey.Enter)
            {
            }
        }
    }
}
