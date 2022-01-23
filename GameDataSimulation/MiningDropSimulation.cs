using RavenNest.BusinessLogic.Game.Processors.Tasks;

namespace GameDataSimulation
{
    public class MiningDropSimulation
    {
        protected readonly Random Random = new Random();

        public static void Run()
        {

            MiningDropSimulation drt = new();

            var miningLevel = 332;
            var hours = 6;
            var scale = 1000;

            //DropRateTest.DropChanceIncrement = 0.00025;
            //DropRateTest.InitDropChance = 0.33;

            ItemDropRateSettings.DropChanceIncrement = 0.00025;
            ItemDropRateSettings.InitDropChance = 0.33;

            var start = DateTime.UtcNow;
            Console.WriteLine("Starting mining simulation");
            Console.WriteLine("--------------------------");
            Console.WriteLine(" Level: " + miningLevel);
            Console.WriteLine(" Simulated Time: " + hours + " hours");
            Console.WriteLine(" Time Scale: x" + scale);
            Console.WriteLine(" DCI: " + (ItemDropRateSettings.DropChanceIncrement * 100) + "%");
            Console.WriteLine(" IDC: " + (ItemDropRateSettings.InitDropChance * 100) + "%");
            Console.WriteLine("");
            Console.WriteLine("--- Progress -------------");

            var left = Console.CursorLeft;
            var top = Console.CursorTop;

            var time = TimeSpan.FromHours(hours);
            var progress = 0.0;
            var expectedOreCount = (double)(time.TotalSeconds / ItemDropRateSettings.ResourceGatherInterval);

            var result = drt.Test(miningLevel, time, scale, (ctx, timeLeft) =>
            {

                progress = (int)((ctx.Ore / expectedOreCount) * 100);
                Console.CursorLeft = left;
                Console.CursorTop = top;
                Console.WriteLine(" Progress: " + progress + "%       ");
                Console.WriteLine(" Time Left: ~" + (int)(timeLeft.TotalSeconds / scale) + "s       ");

            });

            var elapsed = DateTime.UtcNow - start;

            Console.WriteLine("");
            Console.WriteLine("--- Results --------------");
            Console.WriteLine("Simulation ended after " + elapsed.TotalSeconds + " seconds.");
            var totalDrops = result.Inventory.Sum(x => x.Value);
            Console.WriteLine("Total drops: " + totalDrops);
            Console.WriteLine("");
            Console.WriteLine(result.Inventory.Count + " kinds of drops");
            foreach (var item in result.Inventory.OrderByDescending(x => ResourceTaskProcessor.DroppableResources.FirstOrDefault(y => y.Name == x.Key).SkillLevel))
            {
                var i = ResourceTaskProcessor.DroppableResources.FirstOrDefault(y => y.Name == item.Key);
                var dropChance = i.GetDropChance(miningLevel);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write(" * " + item.Key.PadRight(20, ' '));
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("x" + (item.Value).ToString().PadRight(3, ' '));
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write((Math.Round((dropChance * 100), 2) + "%").PadLeft(7, ' '));
                Console.WriteLine();
            }
        }

        public ResourceContext Test(int miningLevel, TimeSpan duration, double timeScale = 1.0, Action<ResourceContext, TimeSpan> onTick = null)
        {
            var ctx = new ResourceContext
            {
                MiningLevel = miningLevel
            };

            Time.Scale = timeScale;
            Time.Reset();
            var now = Time.Now;
            var timeLeft = duration;
            var expectedOreCount = (int)(duration.TotalSeconds / ItemDropRateSettings.ResourceGatherInterval);
            do
            {
                timeLeft = duration - (Time.Now - now);
                Time.Update();
                Update(ctx, onTick, timeLeft);
                System.Threading.Thread.Sleep(1);
            } while (ctx.Ore < expectedOreCount);//timeLeft.TotalSeconds > 0);

            onTick?.Invoke(ctx, TimeSpan.Zero);
            return ctx;
        }

        public void Update(ResourceContext ctx, Action<ResourceContext, TimeSpan> onTick, TimeSpan timeLeft)
        {
            UpdateResourceGain(ctx, () =>
            {
                onTick?.Invoke(ctx, timeLeft);

                AddDrop(ctx.MiningLevel, itemDropped =>
                {
                    ctx.AddItem(itemDropped);
                });

                ++ctx.Ore;
            });
        }

        protected void UpdateResourceGain(ResourceContext ctx, Action onUpdate)
        {
            var now = Time.UtcNow;
            var elapsed = now - ctx.LastTaskUpdate;
            var interval = TimeSpan.FromSeconds(ItemDropRateSettings.ResourceGatherInterval);
            var firstTime = ctx.LastTaskUpdate == DateTime.MinValue;
            while (firstTime || (elapsed = elapsed.Add(-interval)) >= TimeSpan.Zero)
            {
                ctx.LastTaskUpdate = Time.UtcNow;
                onUpdate?.Invoke();
                if (firstTime) break;
            }
        }

        protected void AddDrop(int miningLevel, Action<ResourceDrop> onDrop)
        {
            var multiDrop = Random.NextDouble();
            var isMultiDrop = multiDrop <= 0.1;
            var chance = Random.NextDouble();
            if (chance <= ItemDropRateSettings.InitDropChance)
            {
                foreach (var res in ResourceTaskProcessor.DroppableResources.OrderByDescending(x => x.SkillLevel))
                {
                    chance = Random.NextDouble();
                    if (miningLevel >= res.SkillLevel && (chance <= res.GetDropChance(miningLevel)))
                    {
                        onDrop(res);

                        //IncrementItemStack(gameData, inventoryProvider, session, character, res.Id);
                        if (isMultiDrop)
                        {
                            isMultiDrop = false;
                            continue;
                        }
                        break;
                    }
                }
            }

        }

        public class ResourceContext
        {
            public int Ore;
            public int MiningLevel;
            public DateTime LastTaskUpdate;

            public readonly Dictionary<string, int> Inventory = new Dictionary<string, int>();

            internal void AddItem(ResourceDrop itemDropped)
            {
                Inventory.TryGetValue(itemDropped.Name, out var amount);
                Inventory[itemDropped.Name] = ++amount;
            }
        }
    }
}
