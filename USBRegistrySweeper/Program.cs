using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace USBRegistrySweeper
{
    class Program
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegQueryInfoKey(
            IntPtr hKey, IntPtr lpClass, IntPtr lpcClass, IntPtr lpReserved,
            IntPtr lpcSubKeys, IntPtr lpcMaxSubKeyLen, IntPtr lpcMaxClassLen,
            IntPtr lpcValues, IntPtr lpcMaxValueNameLen, IntPtr lpcMaxValueLen,
            IntPtr lpcSecurityDescriptor, ref FILETIME lpftLastWriteTime);

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        static void Main(string[] args)
        {
            Console.Title = "USBRegistrySweeper";
            string exePath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            if (File.Exists("psexec.exe") && !Environment.CommandLine.ToLower().Contains("psexec"))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "psexec.exe",
                    Arguments = $"-accepteula -i -s \"{Process.GetCurrentProcess().MainModule.FileName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                Environment.Exit(0);
            }

            DisplayHeader();

            while (true)
            {
                DisplayMenu();
                Console.Write("Ваш выбор: ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                string choice = Console.ReadLine();
                Console.ResetColor();

                switch (choice)
                {
                    case "1": ShowUSBDevices(exePath); break;
                    case "2": ClearUSBRegistry(exePath, "USBSTOR"); break;
                    case "3": ClearUSBRegistry(exePath, "USB"); break;
                    case "4": ClearUSBRegistry(exePath, "USBPRINT"); break;
                    case "0":
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\nСпасибо за использование USBRegistrySweeper! До встречи!");
                        Console.ResetColor();
                        return;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Неверный выбор. Пожалуйста, выберите 0-4.");
                        Console.ResetColor();
                        break;
                }
            }
        }

        static void DisplayHeader()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("  +-----------------------------------------+");
            Console.WriteLine("  |    USBRegistrySweeper by d-k-dev       |");
            Console.WriteLine("  +-----------------------------------------+");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  GitHub: https://github.com/d-k-dev");
            Console.WriteLine("  Version 1.0 | Date: 2025-02-20");
            Console.ResetColor();
            Console.WriteLine("  =========================================");
        }

        static void DisplayMenu()
        {
            Console.WriteLine("\nДоступные действия:");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [1] Показать историю USB-устройств и сохранить в файл");
            Console.WriteLine("  [2] Очистить реестр USBSTOR");
            Console.WriteLine("  [3] Очистить реестр USB");
            Console.WriteLine("  [4] Очистить реестр USBPRINT");
            Console.WriteLine("  [0] Выход");
            Console.ResetColor();
        }

        static void ShowUSBDevices(string exePath)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[ Поиск истории USB-устройств... ]");
            Console.ResetColor();

            using (RegistryKey usbStorKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USBSTOR"))
            {
                if (usbStorKey == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Раздел реестра USBSTOR не найден.");
                    Console.ResetColor();
                    Console.ReadLine();
                    return;
                }

                string[] usbDevices = usbStorKey.GetSubKeyNames();
                if (usbDevices.Length == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("USB-устройства не найдены.");
                    Console.ResetColor();
                    Console.ReadLine();
                    return;
                }

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string machineName = Environment.MachineName;
                string historyPath = Path.Combine(exePath, "UsbHistory", machineName);
                string filePath = Path.Combine(historyPath, $"USBHistory_{timestamp}.txt");

                Directory.CreateDirectory(historyPath);

                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("USBRegistrySweeper - История USB-устройств");
                    writer.WriteLine($"Компьютер: {machineName}");
                    writer.WriteLine($"Дата: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine("===================================");

                    int count = 0;
                    foreach (string device in usbDevices)
                    {
                        using (RegistryKey subKey = usbStorKey.OpenSubKey(device))
                        {
                            if (subKey != null)
                            {
                                foreach (string serial in subKey.GetSubKeyNames())
                                {
                                    using (RegistryKey deviceParams = subKey.OpenSubKey(serial))
                                    {
                                        if (deviceParams != null)
                                        {
                                            string name = (string)deviceParams.GetValue("FriendlyName");
                                            if (!string.IsNullOrEmpty(name))
                                            {
                                                count++;
                                                FILETIME ft = new FILETIME();
                                                RegQueryInfoKey(deviceParams.Handle.DangerousGetHandle(),
                                                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                                                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                                                    IntPtr.Zero, IntPtr.Zero, ref ft);

                                                long fileTime = ((long)ft.dwHighDateTime << 32) | (long)ft.dwLowDateTime;
                                                string lastWriteTime = DateTime.FromFileTime(fileTime).ToString("yyyy-MM-dd HH:mm:ss");

                                                Console.ForegroundColor = ConsoleColor.Cyan;
                                                Console.WriteLine($"\nУстройство #{count}");
                                                Console.ResetColor();
                                                Console.WriteLine($"  Название:         {name}");
                                                Console.WriteLine($"  Последнее подкл.: {lastWriteTime}");

                                                writer.WriteLine($"\nУстройство #{count}");
                                                writer.WriteLine($"  Название:         {name}");
                                                writer.WriteLine($"  Последнее подкл.: {lastWriteTime}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\nВсего найдено устройств: {count}");
                    Console.WriteLine($"Данные сохранены в файл: {filePath}");
                    Console.ResetColor();
                    writer.WriteLine($"\nВсего найдено устройств: {count}");
                }
            }
            Console.WriteLine("Нажмите Enter...");
            Console.ReadLine();
        }

        static void ClearUSBRegistry(string exePath, string section)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[ Очистка реестра {section}... ]");
            Console.ResetColor();

            string regPath = $@"SYSTEM\CurrentControlSet\Enum\{section}";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(regPath, writable: true))
            {
                if (key == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Раздел реестра {section} не найден.");
                    Console.ResetColor();
                    Console.ReadLine();
                    return;
                }

                string[] subKeys = key.GetSubKeyNames();
                if (subKeys.Length == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Очищать нечего - реестр пуст.");
                    Console.ResetColor();
                    Console.ReadLine();
                    return;
                }

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string machineName = Environment.MachineName;
                string backupPathDir = Path.Combine(exePath, "Backup", machineName);
                string backupPath = Path.Combine(backupPathDir, $"{section}_Backup_{timestamp}.reg"); 
                Directory.CreateDirectory(backupPathDir);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = $@"export HKLM\{regPath} ""{backupPath}"" /y",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }).WaitForExit();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Бэкап сохранён в: {backupPath}");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Вы уверены? (да/нет): ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                string confirmation = Console.ReadLine().ToLower();
                Console.ResetColor();

                if (confirmation == "да")
                {
                    int deletedCount = 0;
                    foreach (string subKey in subKeys)
                    {
                        key.DeleteSubKeyTree(subKey);
                        deletedCount++;
                    }
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Очистка завершена. Удалено: {deletedCount}");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Очистка отменена.");
                    Console.ResetColor();
                }
            }
            Console.WriteLine("Нажмите Enter...");
            Console.ReadLine();
        }
    }
}