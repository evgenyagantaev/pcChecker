using Hardware.Info;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using System.Management.Automation;


namespace pcChecker
{
    internal class Program
    {
        private const int MinimumRAMInGB = 8;
        private const int MinimumSSDSizeInGB = 256;
        private const int MinimumVRAMForDiscreteInGB = 3;
        private const int MinimumVRAMForIntegratedInGB = 1;
        private const int FreeSpaceThresholdInGB = 15;

        static async Task CheckBLESupport()
        {
            var selector = BluetoothLEDevice.GetDeviceSelector();
            var devices = await DeviceInformation.FindAllAsync(selector);
            if (devices.Any())
            {
                Console.WriteLine("BLE is supported.");
            }
            else
            {
                Console.WriteLine("BLE is not supported.");
            }
        }

        static void CheckWiFiSupport()
        {

            bool wifiSupported = NetworkInterface.GetAllNetworkInterfaces()
                .Any(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

            if (wifiSupported)
            {
                Console.WriteLine("Wi-Fi is supported.");
            }
            else
            {
                Console.WriteLine("Wi-Fi is not supported.");
            }
            Console.WriteLine();
        }

        static void CheckTotalFreeSpace()
        {
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            long totalFreeSpaceBytes = allDrives.Where(d => d.IsReady).Sum(d => d.TotalFreeSpace);
            double totalFreeSpaceGB = totalFreeSpaceBytes / (1024.0 * 1024.0 * 1024.0); // Convert to gigabytes

            Console.WriteLine($"Total free space across all drives: {totalFreeSpaceGB:N2} GB");
            Console.WriteLine();
        }

        static void CheckWindowsVersion()
        {
            var os = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem").Get().Cast<ManagementObject>().FirstOrDefault();
            string osCaption = os?["Caption"]?.ToString() ?? "Unknown Operating System";

            Console.WriteLine($"Operating System: {osCaption}");
            Console.WriteLine();
        }

        static void CheckWindowsVersionAndCompatibility()
        {
            var os = new ManagementObjectSearcher("SELECT Version FROM Win32_OperatingSystem").Get().Cast<ManagementObject>().FirstOrDefault();
            string osVersionString = os?["Version"]?.ToString() ?? "Unknown";
            Console.WriteLine($"Operating System Version: {osVersionString}");

            // Преобразование строки версии в числовой формат для сравнения
            Version osVersion = new Version(osVersionString.Split(' ')[0]); // Исправляем, если формат отличается
            Version requiredVersion = new Version(11, 0);

            if (osVersion >= requiredVersion)
            {
                Console.WriteLine("Pass: Operating system version is 11 or newer.");
            }
            else
            {
                Console.WriteLine($"Not pass: The operating system version is older than 11. Current version: {osVersion}. An update is required for full compatibility.");
            }
            Console.WriteLine();
        }

        static void CheckDiskTypeWithPowerShell()
        {
            using (PowerShell PowerShellInstance = PowerShell.Create())
            {
                // скрипт для проверки типа диска
                PowerShellInstance.AddScript("Get-PhysicalDisk | Select-Object DeviceId, MediaType");

                // выполнение скрипта
                Collection<PSObject> PSOutput = PowerShellInstance.Invoke();

                // обработка результатов
                foreach (PSObject outputItem in PSOutput)
                {
                    // если не возникли ошибки и объект не пустой
                    if (outputItem != null)
                    {
                        Console.WriteLine(outputItem.Properties["DeviceId"].Value + " " + outputItem.Properties["MediaType"].Value);
                    }
                }
            }
        }

        static string GetCurrentProcessorName()
        {
            string processorName = "";
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_Processor");
            foreach (var item in searcher.Get())
            {
                processorName = item["Name"].ToString();
                break; // Assuming single processor system
            }
            return processorName;
        }

        static bool CheckProcessorCompatibility(string processorName)
        {
            // Определение серии процессора (i3, i5, i7) и поколения
            if (processorName.Contains("i7"))
            {
                return true; // i7 считаем подходящим
            }
            else if (processorName.Contains("i5"))
            {
                // Определение поколения для i5
                int generation = ExtractGeneration(processorName);
                return generation >= 13; // подходит, если поколение больше или равно 13
            }
            else if (processorName.Contains("i3"))
            {
                return false; // i3 считаем заведомо слабее
            }

            return false; // Все остальные случаи считаем неподходящими
        }

        static int ExtractGeneration(string processorName)
        {
            // Предполагаем, что название модели содержит поколение в формате "iX-XXXX"
            var parts = processorName.Split(new[] { 'i', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (int.TryParse(part, out int numericPart))
                {
                    int generation = numericPart / 1000; // Для получения поколения делим число модели на 1000
                    return generation;
                }
            }
            return 0; // Возвращаем 0, если поколение не обнаружено
        }

        static void Main(string[] args)
        {
            var hardwareInfo = new HardwareInfo();

            hardwareInfo.RefreshAll();

            //CheckWindowsVersion();
            CheckWindowsVersionAndCompatibility();

            Console.WriteLine("Processors:");
            foreach (var cpu in hardwareInfo.CpuList)
            {
                Console.WriteLine($"Name: {cpu.Name}");
                Console.WriteLine($"Number of cores: {cpu.NumberOfCores}");
                Console.WriteLine($"Frequency: {cpu.MaxClockSpeed}MHz");
                Console.WriteLine();
            }

            var processorName = GetCurrentProcessorName();
            Console.WriteLine($"Detected Processor: {processorName}");

            var compatibility = CheckProcessorCompatibility(processorName);
            Console.WriteLine(compatibility ? "Your processor is suitable." : "Your processor is not suitable.");
            Console.WriteLine();

            // Correctly calculate total RAM by explicitly working with long
            long totalMemory = hardwareInfo.MemoryList.Sum(memory => (long)memory.Capacity);
            Console.WriteLine("Memory:");
            Console.WriteLine($"Total RAM: {totalMemory / 1024 / 1024 / 1024}GB");

            // Проверка на достаточность оперативной памяти
            long totalMemoryGB = totalMemory / 1024 / 1024 / 1024; // Пересчет в ГБ
            if (totalMemoryGB > 8)
            {
                Console.WriteLine("The amount of RAM is sufficient.");
            }
            else
            {
                Console.WriteLine("The amount of RAM is not sufficient. Upgrade recommended.");
            }
            Console.WriteLine();

            Console.WriteLine("Drives:");
            foreach (var drive in hardwareInfo.DriveList)
            {
                Console.WriteLine($"Name: {drive.Model}");
                Console.WriteLine($"Type: {drive.Description}");
                Console.WriteLine($"Size: {drive.Size / 1024 / 1024 / 1024}GB");
                Console.WriteLine();
            }
            CheckDiskTypeWithPowerShell();
            CheckTotalFreeSpace();

            Console.WriteLine("Video Cards:");
            foreach (var videoCard in hardwareInfo.VideoControllerList)
            {
                Console.WriteLine($"Name: {videoCard.Name}");
                Console.WriteLine($"Manufacturer: {videoCard.Manufacturer}");
                Console.WriteLine($"Memory: {videoCard.AdapterRAM / 1024 / 1024}MB");
                Console.WriteLine();
            }

            _ = CheckBLESupport();

            CheckWiFiSupport();


        }
    }
}
