using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Permissions;

namespace PlayerObserver
{
    class Program
    {
        private static void Main()
        {
            var monitor = new ConfigurationMonitor();

            var player = new Player(10,1,1);
            var fileStorage = new FileStorage(player);

            monitor.Run();

            monitor.Subscribe(fileStorage);
            player.ApplyDamage(2f);

            Console.ReadLine();
            monitor.Unsubscribe(fileStorage);
        }
    }

    public class ConfigurationMonitor
    {
        private readonly List<FileStorage> _storageList = new List<FileStorage>();
        private readonly FileSystemWatcher _watcher = new FileSystemWatcher(Directory.GetCurrentDirectory(), "*.data");

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public void Run()
        {
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.EnableRaisingEvents = true;
        }

        public void Subscribe(FileStorage storage)
        {
            _storageList.Add(storage);
        }

        public void Unsubscribe(FileStorage storage)
        {
            _storageList.Remove(storage);
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            Console.WriteLine($"Файл: {e.FullPath} {e.ChangeType}");

            if (e.ChangeType == WatcherChangeTypes.Changed || e.ChangeType == WatcherChangeTypes.Created)
                foreach (var storage in _storageList)
                    if (e.Name.Equals(storage.GetStorageName()))
                        storage.Player.Load();
        }
    }

    public class Player
    {
        private float _armor;

        public event EventHandler<PlayerInfo> OnLoad;
        public event Action OnSave;

        public Player(float health, float armor, int id)
        {
            Health = health;
            _armor = armor;
            Id = id;

            Load();
        }

        public int Id { get; }

        public float Health { get; private set; }

        public void Load()
        {
            var playerInfo = new PlayerInfo(Health);

            OnLoad?.Invoke(this, playerInfo);
            Health = playerInfo.Health;
        }

        public void ApplyDamage(float damage)
        {
            var healthDelta = damage - _armor;
            Health -= healthDelta;
            _armor /= 2;

            Console.WriteLine($"Вы получили урона - {healthDelta}");
            OnSave?.Invoke();
        }
    }

    public class PlayerInfo : EventArgs
    {
        public PlayerInfo(float health)
        {
            Health = health;
        }

        public float Health { get; set; }
    }

    public class FileStorage : IDisposable
    {
        private static readonly List<int> Players = new List<int>();

        public FileStorage(Player player)
        {
            Player = player;

            try
            {
                if (Players.Contains(player.Id))
                    throw new InvalidOperationException($"Для игрока с ID={Player.Id} уже создано файловое хранилище!");

                Players.Add(Player.Id);
                Player.OnLoad += OnLoad;
                Player.OnSave += OnSave;

                Player.Load();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public Player Player { get; }

        private void OnSave()
        {
            File.WriteAllText(GetStorageName(), Player.Health.ToString(CultureInfo.InvariantCulture));

            Console.WriteLine($"Конфигурация {GetStorageName()} сохранена!");
        }

        public void OnLoad(object sender, PlayerInfo playerInfo)
        {
            if (!File.Exists(GetStorageName())) return;

            var data = File.ReadAllText(GetStorageName());
            if (float.TryParse(data, out var parseResult)) playerInfo.Health = parseResult;

            Console.WriteLine($"Конфигурация {GetStorageName()} загружена!");
        }

        public void Dispose()
        {
            Player.OnLoad -= OnLoad;
            Player.OnSave -= OnSave;
            Players.Remove(Player.Id);
        }

        public string GetStorageName()
        {
            return $"user_{Player.Id}.data";
        }
    }
}
