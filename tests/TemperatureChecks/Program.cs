using System.Reflection;
using LibreHardwareMonitor.Hardware;

var repositoryType = Assembly.Load("RunCat 365").GetType("RunCat365.TemperatureRepository", throwOnError: true)!;
var flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
var read = repositoryType.GetMethod("TryReadCelsius", flags)!;

foreach (var (name, value, sensorType, expected) in new (string, float?, SensorType, bool)[]
{
    ("CPU Package", 65, SensorType.Temperature, true),
    ("CPU Core #1", 55, SensorType.Temperature, true),
    ("CPU Core #1 Distance to TjMax", 45, SensorType.Temperature, false),
    ("CPU Package", null, SensorType.Temperature, false),
    ("CPU Package", float.NaN, SensorType.Temperature, false),
    ("CPU Package", float.PositiveInfinity, SensorType.Temperature, false),
    ("CPU Package", float.NegativeInfinity, SensorType.Temperature, false),
    ("CPU Package", -51, SensorType.Temperature, false),
    ("CPU Package", 151, SensorType.Temperature, false),
    ("CPU Package", 60, SensorType.Load, false),
})
{
    var sensor = DispatchProxy.Create<ISensor, SensorProxy>();
    var proxy = (SensorProxy)sensor;
    proxy.Name = name;
    proxy.Value = value;
    proxy.SensorType = sensorType;
    object?[] arguments = [sensor, 0f];
    var accepted = (bool)read.Invoke(null, arguments)!;
    if (accepted != expected || (accepted && (float)arguments[1]! != value))
        throw new InvalidOperationException($"Unexpected reading: {name}={value}, accepted={accepted}");
}
Console.WriteLine("PASS: temperature filtering (10 cases).");

var rangeType = repositoryType.Assembly.GetType("RunCat365.TemperatureRange", throwOnError: true)!;
var createRange = rangeType.GetMethod("Create", flags)!;
var toLoad = rangeType.GetMethod("ToLoad", flags)!;
var idleCelsius = rangeType.GetProperty("IdleCelsius", flags)!;
var fullCelsius = rangeType.GetProperty("FullCelsius", flags)!;

var rangeCases = new (int Idle, int Full, int ExpectedIdle, int ExpectedFull)[]
{
    (40, 95, 40, 95),
    (50, 52, 50, 55),
    (90, 60, 90, 95),
    (-20, 300, 0, 120),
    (119, 119, 115, 120),
    (0, 0, 0, 5),
};
foreach (var (idle, full, expectedIdle, expectedFull) in rangeCases)
{
    var range = createRange.Invoke(null, [idle, full])!;
    var actualIdle = (int)idleCelsius.GetValue(range)!;
    var actualFull = (int)fullCelsius.GetValue(range)!;
    if (actualIdle != expectedIdle || actualFull != expectedFull)
        throw new InvalidOperationException($"Unexpected range: Create({idle}, {full}) = {actualIdle}..{actualFull}");
}

var loadCases = new (int Idle, int Full, float Celsius, float ExpectedLoad)[]
{
    (40, 95, 40f, 0f),
    (40, 95, 95f, 100f),
    (40, 95, 20f, 0f),
    (40, 95, 120f, 100f),
    (40, 95, 67.5f, 50f),
    (50, 70, 55f, 25f),
    (30, 35, 34f, 80f),
};
foreach (var (idle, full, celsius, expectedLoad) in loadCases)
{
    var range = createRange.Invoke(null, [idle, full])!;
    var actualLoad = (float)toLoad.Invoke(range, [celsius])!;
    if (Math.Abs(actualLoad - expectedLoad) > 0.01f)
        throw new InvalidOperationException($"Unexpected load: {idle}..{full} at {celsius} = {actualLoad}");
}
Console.WriteLine($"PASS: temperature range ({rangeCases.Length + loadCases.Length} cases).");

if (args.Contains("--probe"))
{
    Console.WriteLine($"PawnIO ready: {repositoryType.GetProperty("IsPawnIoReady", flags)!.GetValue(null)}");
    var repository = Activator.CreateInstance(repositoryType, nonPublic: true)!;
    var validSamples = 0;
    try
    {
        for (var index = 0; index < 5; index++)
        {
            repositoryType.GetMethod("Update", flags)!.Invoke(repository, null);
            var sample = repositoryType.GetMethod("Get", flags)!.Invoke(repository, null);
            if (sample is null)
            {
                Console.WriteLine($"Sample {index + 1}: unavailable");
            }
            else
            {
                var type = sample.GetType();
                var maximum = (float)type.GetProperty("MaximumCelsius", flags)!.GetValue(sample)!;
                var average = (float)type.GetProperty("AverageCelsius", flags)!.GetValue(sample)!;
                var source = type.GetProperty("Source", flags)!.GetValue(sample);
                if (!float.IsFinite(maximum) || !float.IsFinite(average))
                    throw new InvalidOperationException("Non-finite live temperature.");
                Console.WriteLine($"Sample {index + 1}: source={source}, max={maximum:F1}C, average={average:F1}C");
                validSamples++;
            }
            Thread.Sleep(1000);
        }
    }
    finally
    {
        repositoryType.GetMethod("Close", flags)!.Invoke(repository, null);
    }
    if (validSamples != 5) Environment.ExitCode = 1;
}

public class SensorProxy : DispatchProxy
{
    public string Name { get; set; } = "";
    public float? Value { get; set; }
    public SensorType SensorType { get; set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
        "get_Name" => Name,
        "get_Value" => Value,
        "get_SensorType" => SensorType,
        _ => throw new NotSupportedException(targetMethod?.Name),
    };
}
