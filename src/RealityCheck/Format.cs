using System;
using System.Globalization;

namespace RealityCheck
{
    internal static class Fmt
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>3 significant digits with K/M/B/T suffix: 8.93M, 412, 0.52.</summary>
        public static string Num(double v)
        {
            if (double.IsNaN(v)) return "?";
            if (double.IsInfinity(v)) return "∞";
            double a = Math.Abs(v);
            string sign = v < 0 ? "-" : "";
            if (a >= 1e15) return sign + (a / 1e15).ToString(a >= 1e17 ? "0" : "0.#", Inv) + " quadrillion";
            if (a >= 1e12) return sign + (a / 1e12).ToString(a >= 1e14 ? "0" : "0.#", Inv) + " trillion";
            if (a >= 1e9) return sign + (a / 1e9).ToString(a >= 1e11 ? "0" : "0.#", Inv) + " billion";
            if (a >= 1e6) return sign + (a / 1e6).ToString(a >= 1e8 ? "0" : "0.#", Inv) + " million";
            if (a >= 1000)
            {
                double mag = Math.Pow(10, Math.Floor(Math.Log10(a)) - 2);   // 3 significant digits
                return sign + (Math.Round(a / mag) * mag).ToString("#,0", Inv).Replace(",", " ");
            }
            string f = a >= 100 ? "0" : a >= 10 ? "0.#" : a >= 1 ? "0.##" : "0.###";
            return v.ToString(f, Inv);
        }

        public static string Mass(double kg, WeightUnits u)
        {
            double t = kg / 1000;
            string metric = kg < 1 ? Num(kg * 1000) + " g" : kg < 1000 ? Num(kg) + " kg" :
                t < 1e3 ? Num(t) + " t" : t < 1e6 ? Num(t / 1e3) + " kt" : t < 1e9 ? Num(t / 1e6) + " Mt" :
                t < 1e12 ? Num(t / 1e9) + " Gt" : Num(t / 1e12) + " Tt";
            double lb = kg / 0.45359237;
            string imperial = lb < 1 ? Num(lb * 16) + " oz" : Num(lb) + " lb";
            return Both(metric, imperial, u);
        }

        public static string Length(double m, WeightUnits u)
        {
            string metric = m < 0.01 ? Num(m * 1000) + " mm" : m < 1 ? Num(m * 100) + " cm" : m < 1000 ? Num(m) + " m" : Num(m / 1000) + " km";
            double ft = m / 0.3048;
            string imperial = ft < 1 ? Num(ft * 12) + " in" : ft < 5280 ? Num(ft) + " ft" : Num(ft / 5280) + " mi";
            return Both(metric, imperial, u);
        }

        public static string Area(double m2, WeightUnits u)
        {
            string metric = m2 < 1 ? Num(m2 * 10000) + " cm²" : m2 < 1e6 ? Num(m2) + " m²" : Num(m2 / 1e6) + " km²";
            double ft2 = m2 / 0.09290304;
            string imperial = ft2 < 1 ? Num(ft2 * 144) + " in²" : m2 < 2.59e6 ? Num(ft2) + " ft²" : Num(m2 / 2.59e6) + " mi²";
            return Both(metric, imperial, u);
        }

        public static string Volume(double m3, WeightUnits u)
        {
            string metric = m3 < 1 ? Num(m3 * 1000) + " L" : m3 < 1e9 ? Num(m3) + " m³" : Num(m3 / 1e9) + " km³";
            string imperial = Num(m3 * 264.172) + " gal";
            return Both(metric, imperial, u);
        }

        public static string Pressure(double pa, WeightUnits u)
        {
            string metric = pa < 1e6 ? Num(pa / 1000) + " kPa" : pa < 1e9 ? Num(pa / 1e6) + " MPa" : Num(pa / 1e9) + " GPa";
            return Both(metric, Num(pa / 6894.757) + " psi", u);
        }

        public static string Speed(double ms, WeightUnits u) => Both(Num(ms * 3.6) + " km/h", Num(ms * 2.23694) + " mph", u);

        public static string Time(double s) =>
            s < 1 ? Num(s * 1000) + " ms" : s < 120 ? Num(s) + " s" : Num(s / 60) + " min";

        public static string Temp(double c, WeightUnits u) =>
            Both(c.ToString("0", Inv) + " °C", (c * 9 / 5 + 32).ToString("0", Inv) + " °F", u);

        /// <summary>Temperature with one decimal (body temperature).</summary>
        public static string Temp1(double c, WeightUnits u) =>
            Both(c.ToString("0.0", Inv) + " °C", (c * 9 / 5 + 32).ToString("0.0", Inv) + " °F", u);

        public static string Wind(double ms) =>
            ms < 3 ? "a light breeze" :
            ms < 10 ? "a fresh breeze" :
            ms < 20 ? "a strong wind" :
            ms < 33 ? "a storm" :
            ms < 70 ? "a hurricane" :
            ms < 140 ? "the strongest tornado" :
            ms < 343 ? "a jet engine blast" :
            "a supersonic shock wave";

        public static string Factor(double k) => "×" + Num(k);

        private static string Both(string metric, string imperial, WeightUnits u) => u switch
        {
            WeightUnits.Metric => metric,
            WeightUnits.Imperial => imperial,
            _ => metric + " / " + imperial,
        };

        // --- "about the size of …" ------------------------------------------------------
        // Ladders cover the whole game range: the player seen by a 23 km giantess (≈0.1 mm, ≈30 ng)
        // up to what that giantess weighs, drinks or shakes. Steps are roughly ×2 … ×4 apart.
        private static readonly (double size, string en)[] Lengths =
        {
            (0.000002, "a bacterium"),
            (0.000008, "a red blood cell"),
            (0.00003, "a pollen grain"),
            (0.00007, "hair width"),
            (0.00015, "an amoeba"),
            (0.0003, "a dust mite"),
            (0.0005, "a grain of salt"),
            (0.001, "a grain of sand"),
            (0.0015, "a pinhead"),
            (0.002, "a flea"),
            (0.003, "a sesame seed"),
            (0.005, "an ant"),
            (0.007, "a rice grain"),
            (0.01, "a ladybug"),
            (0.014, "a bee"),
            (0.019, "a fingernail"),
            (0.024, "a coin"),
            (0.033, "a paperclip"),
            (0.043, "a golf ball"),
            (0.05, "a matchbox"),
            (0.07, "a hamster"),
            (0.09, "a mouse"),
            (0.15, "a smartphone"),
            (0.3, "a cat"),
            (0.6, "a small dog"),
            (1.1, "a child"),
            (1.75, "an adult"),
            (3.5, "an elephant"),
            (5.5, "a giraffe"),
            (9, "a house"),
            (12, "a bus"),
            (30, "tower block"),
            (70, "a jumbo jet"),
            (105, "a football pitch"),
            (269, "the Titanic"),
            (330, "Eiffel Tower"),
            (443, "Empire State"),
            (830, "Burj Khalifa"),
            (1_280, "Golden Gate span"),
            (2_700, "Golden Gate"),
            (4_800, "Mont Blanc"),
            (8_849, "Mount Everest"),
            (11_000, "airliner height"),
            (20_000, "ozone layer"),
            (39_000, "record skydive"),
            (34_000, "the Strait of Dover"),
            (100_000, "edge of space"),
            (408_000, "ISS orbit"),
            (446_000, "the Grand Canyon"),
            (1_430_000, "London–Rome"),
            (2_300_000, "Barrier Reef"),
            (3_474_000, "the Moon"),
            (12_742_000, "the Earth"),
            (384_400_000, "the distance to the Moon"),
        };

        private static readonly (double m2, string en)[] Areas =
        {
            (0.0006, "a postage stamp"),
            (0.0046, "a credit card"),
            (0.012, "a phone screen"),
            (0.025, "a paperback"),
            (0.06, "an A4 sheet"),
            (0.15, "a pizza box"),
            (0.5, "a doormat"),
            (2, "a single bed"),
            (4, "a double bed"),
            (12, "a parking space"),
            (30, "a living room"),
            (70, "a flat"),
            (150, "a house"),
            (260, "a tennis court"),
            (420, "a basketball court"),
            (1_250, "an Olympic pool"),
            (1_800, "an ice hockey rink"),
            (4_000, "a supermarket"),
            (7_140, "a football pitch"),
            (24_000, "the Colosseum"),
            (53_000, "Pyramid base"),
            (160_000, "shopping mall"),
            (340_000, "Disneyland Park"),
            (1_420_000, "Hyde Park"),
            (3_410_000, "Central Park"),
            (1.23e7, "Heathrow Airport"),
            (5.9e7, "Manhattan"),
            (1.05e8, "Paris"),
            (5.8e8, "Lake Geneva"),
            (7.83e8, "New York City"),
            (1.57e9, "Greater London"),
            (4.9e9, "Grand Canyon park"),
            (1.36e10, "Greater Tokyo"),
            (5.8e10, "Lake Michigan"),
            (2.44e11, "Great Lakes"),
            (3.44e11, "Barrier Reef"),
            (1.3e12, "Gobi"),
            (5.5e12, "Amazon"),
            (9.2e12, "Sahara"),
            (1.4e13, "Antarctica"),
            (1.65e14, "Pacific"),
            (5.1e14, "Earth"),
        };

        private static readonly (double kg, string en)[] Masses =
        {
            (1e-12, "human cell"),
            (1e-11, "a pollen grain"),
            (1e-8, "a dust mite"),
            (1.7e-7, "a grain of sand"),
            (5e-7, "a flea"),
            (3e-7, "a grain of salt"),
            (3e-6, "an ant"),
            (1.2e-5, "a housefly"),
            (2.5e-5, "a grain of rice"),
            (4e-5, "a ladybug"),
            (1e-4, "a honeybee"),
            (2e-4, "a feather"),
            (1e-3, "a paperclip"),
            (2.5e-3, "a sugar cube"),
            (5e-3, "a grape"),
            (7.5e-3, "a coin"),
            (0.045, "a golf ball"),
            (0.025, "a mouse"),
            (0.2, "a smartphone"),
            (1, "a bag of sugar"),
            (4, "a cat"),
            (25, "a big dog"),
            (75, "an adult"),
            (300, "a grand piano"),
            (500, "a horse"),
            (1_500, "a car"),
            (6_000, "an elephant"),
            (13_000, "a bus"),
            (40_000, "a loaded truck"),
            (150_000, "a blue whale"),
            (400_000, "a jumbo jet"),
            (1_500_000, "a freight train"),
            (3e6, "a Saturn V rocket"),
            (7_300_000, "Eiffel Tower"),
            (1.87e7, "a nuclear submarine"),
            (5.2e7, "the Titanic"),
            (1e8, "an aircraft carrier"),
            (3.3e8, "Empire State"),
            (4.5e8, "Burj Khalifa"),
            (1.5e9, "an iceberg"),
            (6e9, "Great Pyramid"),
            (2e10, "a big iceberg"),
            (6.5e10, "Three Gorges Dam"),
            (2e11, "a huge iceberg"),
            (5e11, "all humans"),
            (1.45e12, "a week of Niagara water"),
            (3.1e12, "2 weeks of Niagara water"),
            (7.4e12, "Loch Ness water"),
            (2.2e13, "Lake Como water"),
            (8.9e13, "Lake Geneva water"),
            (1.5e14, "Lake Tahoe water"),
            (4.8e14, "Lake Erie water"),
            (2e14, "a large mountain"),
            (8e14, "Mount Everest"),
        };

        // One dense volume ladder (×2 … ×4 steps) from a teaspoon to all the oceans – used for every
        // amount of water, so there are no tiny percentages and no huge multipliers.
        private static readonly (double m3, string en)[] Volumes =
        {
            (5e-6, "a teaspoon"),
            (1.5e-5, "a tablespoon"),
            (3e-5, "an espresso"),
            (8e-5, "a small cup"),
            (0.00025, "a glass of water"),
            (0.001, "a bottle of water"),
            (0.01, "a bucket"),
            (0.04, "a big cauldron"),
            (0.159, "an oil barrel"),
            (0.3, "a bathtub"),
            (1, "a hot tub"),
            (4, "a paddling pool"),
            (3, "a car interior"),
            (10, "a garden pond"),
            (35, "a tanker truck"),
            (75, "a backyard pool"),
            (100, "a rail tank car"),
            (300, "a big swimming pool"),
            (400, "a family house"),
            (1_000, "a water tower"),
            (2_500, "an Olympic pool"),
            (6_000, "a big water tower"),
            (15_000, "a city water tank"),
            (20_000, "a cathedral"),
            (86_650, "Albert Hall"),
            (200_000, "Hindenburg"),
            (320_000, "supertanker"),
            (1.1e6, "Wembley"),
            (3.8e6, "Central Park pond"),
            (1e7, "a large reservoir"),
            (4.3e7, "5 h of Niagara"),
            (2.07e8, "1 day of Niagara"),
            (5.62e8, "Sydney Harbour"),
            (2e9, "10 days of Niagara"),
            (7.4e9, "Loch Ness"),
            (2.24e10, "Lake Como"),
            (8.9e10, "Lake Geneva"),
            (1.51e11, "Lake Tahoe"),
            (4.8e11, "Lake Erie"),
            (2.75e12, "Lake Victoria"),
            (1.21e13, "Lake Superior"),
            (2.36e13, "Lake Baikal"),
            (7.8e13, "the Caspian Sea"),
            (5.47e14, "the Black Sea"),
            (3.75e15, "the Mediterranean Sea"),
            (1.8e16, "the Arctic Ocean"),
            (3.1e17, "the Atlantic Ocean"),
            (1.335e18, "all the oceans"),
        };

        /// <summary>Any amount of water, e.g. "2× bathtub" or "Loch Ness".</summary>
        // Liquids only: containers, tankers, pools, reservoirs, lakes, seas.
        private static readonly (double m3, string en)[] Liquids =
        {
            (5e-6, "a teaspoon"),
            (0.00025, "a glass"),
            (0.001, "a bottle"),
            (0.005, "a watering can"),
            (0.01, "a bucket"),
            (0.04, "a cauldron"),
            (0.159, "an oil barrel"),
            (0.3, "a bathtub"),
            (1, "a hot tub"),
            (10, "a garden pond"),
            (35, "a tanker truck"),
            (75, "a backyard pool"),
            (100, "a rail tank car"),
            (1_000, "a water tower"),
            (2_500, "an Olympic pool"),
            (6_000, "a big water tower"),
            (40_000, "tanker ship"),
            (120_000, "an oil tanker"),
            (320_000, "a supertanker"),
            (1e6, "a small lake"),
            (3.8e6, "Central Park pond"),
            (1e7, "a large reservoir"),
            (4.3e7, "5 h of Niagara"),
            (2.07e8, "1 day of Niagara"),
            (5.62e8, "Sydney Harbour"),
            (2e9, "10 days of Niagara"),
            (7.4e9, "Loch Ness"),
            (2.24e10, "Lake Como"),
            (8.9e10, "Lake Geneva"),
            (1.51e11, "Lake Tahoe"),
            (4.8e11, "Lake Erie"),
            (2.75e12, "Lake Victoria"),
            (1.21e13, "Lake Superior"),
            (2.36e13, "Lake Baikal"),
            (7.8e13, "the Caspian Sea"),
            (5.47e14, "the Black Sea"),
            (3.75e15, "the Mediterranean"),
            (1.8e16, "the Arctic Ocean"),
            (3.1e17, "the Atlantic"),
            (1.335e18, "all the oceans"),
        };

        /// <summary>Any amount of liquid, e.g. "2× bathtub" or "Loch Ness".</summary>
        public static string Lake(double m3) => Nearest(Liquids, m3);

        private static readonly (double ms, string en)[] Speeds =
        {
            (0.01, "a snail"),
            (0.3, "a tortoise"),
            (1.4, "walking"),
            (3, "jogging"),
            (5.5, "a bicycle"),
            (10, "a sprinter"),
            (14, "town car"),
            (28, "highway car"),
            (50, "race bike"),
            (83, "bullet train"),
            (250, "a jet airliner"),
            (343, "sound speed"),
            (1_000, "a rifle bullet"),
            (2_000, "Mach 6"),
            (7_900, "orbital speed"),
            (11_200, "escape velocity"),
            (30_000, "Earth's orbit"),
        };

        private static readonly (double w, string en)[] Powers =
        {
            (10, "a phone charger"),
            (100, "a light bulb"),
            (1_000, "a microwave"),
            (3_000, "a kettle"),
            (20_000, "a sauna stove"),
            (100_000, "a car engine"),
            (500_000, "a truck engine"),
            (3e6, "a wind turbine"),
            (6e6, "a locomotive"),
            (1e8, "a jumbo jet"),
            (1e9, "a nuclear plant"),
            (4.4e9, "Niagara power"),
            (2.25e10, "largest dam"),
            (1.8e13, "all humanity"),
            (1.7e17, "all sunlight"),
        };

        private static readonly (double pa, string en)[] Pressures =
        {
            (1_000, "a light touch"),
            (15_000, "a standing person"),
            (100_000, "the air around us"),
            (220_000, "a car tyre"),
            (600_000, "a bike tyre"),
            (2e6, "a stiletto heel"),
            (2e7, "a scuba tank"),
            (1.1e8, "the deepest ocean"),
            (1.5e8, "crushing granite"),
            (5e9, "diamond making"),
            (3.6e11, "the Earth's core"),
        };

        // Heights: buildings, then mountains, then layers of the air.
        private static readonly (double m, string en)[] Heights =
        {
            (0.15, "a curb"),
            (0.45, "a chair"),
            (0.9, "a table"),
            (1.75, "an adult"),
            (4, "a house"),
            (10, "townhouse"),
            (30, "tower block"),
            (46, "Statue of Liberty"),
            (60, "high-rise"),
            (96, "Big Ben"),
            (139, "Great Pyramid"),
            (169, "Washington Mon."),
            (330, "Eiffel Tower"),
            (443, "Empire State"),
            (634, "Tokyo Skytree"),
            (828, "Burj Khalifa"),
            (3_776, "Mount Fuji"),
            (4_478, "Matterhorn"),
            (4_808, "Mont Blanc"),
            (6_190, "Denali"),
            (8_849, "Mount Everest"),
            (11_000, "airliner height"),
            (18_000, "Concorde height"),
            (20_000, "ozone layer"),
            (39_000, "record skydive"),
            (50_000, "stratosphere top"),
            (100_000, "edge of space"),
        };

        public static string LikeHeight(double m) => Nearest(Heights, m);

        /// <summary>Rounded count for people-friendly numbers: 6.6 · 47 · 1 500 · 11 000 · 2.3 million.</summary>
        public static string Round(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return "∞";
            if (x <= 0) return "0";
            if (x < 0.1) return "1/" + Round(1 / x);          // e.g. 1/340 of a blue whale
            if (x < 10) return x.ToString(x < 1 ? "0.##" : "0.#", Inv);
            if (x >= 1e12) return (x / 1e12).ToString("0.#", Inv) + " trillion";
            if (x >= 1e9) return (x / 1e9).ToString("0.#", Inv) + " billion";
            if (x >= 1e6) return (x / 1e6).ToString("0.#", Inv) + " million";
            double mag = Math.Pow(10, Math.Floor(Math.Log10(x)) - 1);   // keep 2 significant digits
            return (Math.Round(x / mag) * mag).ToString("#,0", Inv).Replace(",", " ");
        }

        /// <summary>What a quake of this magnitude feels like.</summary>
        public static string QuakeWord(double m) =>
            m < 1 ? "not felt" : m < 2 ? "a truck passing" : m < 3 ? "barely felt" : m < 4 ? "windows rattle" :
            m < 5 ? "objects fall" : m < 6 ? "walls crack" : m < 7 ? "buildings fall" : m < 8 ? "a major quake" :
            m < 9 ? "a great quake" : "biggest ever";

        // Forces (N): people, then machines.
        private static readonly (double n, string en)[] Forces =
        {
            (300, "a hard push"),
            (2_000, "a strong kick"),
            (15_000, "a car on a jack"),
            (100_000, "a crane hook"),
            (500_000, "a jet engine"),
            (2_300_000, "a space shuttle engine"),
            (3.4e7, "Saturn V rocket"),
            (4.9e8, "largest forging press"),
            (5e9, "a supertanker aground"),
        };

        public static string LikeForce(double n) => Nearest(Forces, n);

        public static string TempWord(double c) =>
            c < -40 ? "arctic" : c < -10 ? "freezing" : c < 5 ? "cold" : c < 15 ? "cool" :
            c < 25 ? "mild" : c < 32 ? "warm" : c < 40 ? "hot" : "scorching";

        public static string LikeSpeed(double ms) => Nearest(Speeds, ms);
        public static string LikePower(double w) => Nearest(Powers, w);
        public static string LikePressure(double pa) => Nearest(Pressures, pa);

        // Energy in tonnes of TNT.
        private static readonly (double t, string en)[] Energies =
        {
            (1e-6, "a firecracker"),
            (1.4e-4, "car crash"),
            (2e-4, "a hand grenade"),
            (0.01, "train crash"),
            (0.24, "a lightning bolt"),
            (1, "demolition"),
            (20, "mining blast"),
            (500, "M5 quake"),
            (250_000, "a thunderstorm"),
            (500_000, "Chelyabinsk"),
            (1.5e7, "Tunguska"),
            (2.4e7, "Mount St. Helens"),
            (2e8, "Krakatoa"),
            (1e14, "dino asteroid"),
        };

        public static string LikeEnergy(double tnt) => Nearest(Energies, tnt);

        public static string Tnt(double tons) =>
            tons < 0.001 ? Num(tons * 1e6) + " g TNT" : tons < 1 ? Num(tons * 1000) + " kg TNT" :
            tons < 1000 ? Num(tons) + " t TNT" : tons < 1e6 ? Num(tons / 1000) + " kt TNT" : Num(tons / 1e6) + " Mt TNT";

        public static string Force(double n, WeightUnits u) =>
            Both(n < 1e6 ? Num(n / 1000) + " kN" : n < 1e9 ? Num(n / 1e6) + " MN" : Num(n / 1e9) + " GN",
                 Num(n / 4448.22) + " lbf", u) + $" (~{Mass(n / 9.81, u)})";

        public static string LikeVolume(double m3) => Nearest(Volumes, m3);
        public static string LikeArea(double m2) => Nearest(Areas, m2);

        /// <summary>"12× an Olympic pool" / "3% of Loch Ness" style comparison with a fixed reference.</summary>
        public static string Of(double v, double reference, string en)
        {
            double r = v / reference;
            return r >= 1 ? $"{Num(r)}× {en}" : $"{Num(r * 100)}% of {en}".Replace("  ", " ");
        }

        public static string LikeLength(double m) => Nearest(Lengths, m);
        public static string LikeMass(double kg) => Nearest(Masses, kg);

        /// <summary>
        /// The largest example not bigger than x. Close to it (within ×1.5) → "an elephant";
        /// further → "3× elephant"; below the smallest example → "0.2× bacterium".
        /// </summary>
        private static string Nearest((double v, string en)[] list, double x)
        {
            if (double.IsNaN(x) || x <= 0) return "nothing";
            if (double.IsInfinity(x)) return Bare(list[list.Length - 1].en);
            // Order-independent: the largest example not above x, else the smallest example.
            var best = list[0];
            foreach (var item in list) if (item.v < best.v) best = item;
            foreach (var item in list)
                if (item.v <= x && (best.v > x || item.v > best.v)) best = item;
            double r = x / best.v;
            if (r >= 0.67 && r < 1.5) return Bare(best.en);
            return $"{Num(r)}× {Bare(best.en)}";
        }

        /// <summary>"an elephant" → "elephant", "Eiffel Tower" stays.</summary>
        private static string Bare(string en) =>
            en.StartsWith("a ") ? en.Substring(2) : en.StartsWith("an ") ? en.Substring(3) :
            en.StartsWith("the ") ? en.Substring(4) : en;
    }
}
