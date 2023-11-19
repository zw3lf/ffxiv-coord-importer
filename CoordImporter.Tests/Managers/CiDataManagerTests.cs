using CoordImporter.Managers;

namespace CoordImporter.Tests.Managers;

[TestFixture]
public class CiDataManagerTests
{
    // some names to test with. yes these are copied from the json, but don't think we need to verify ALL names in the json
    private static readonly IReadOnlyDictionary<string, string> TestCorrectedMarkNames = new Dictionary<string, string>()
    {
        { "Dalvags Final Flame", "Dalvag's Final Flame" },
        { "Lil Murderer", "Li'l Murderer" },
        { "Zanigoh", "Zanig'oh" },
    };

    private static readonly IReadOnlyList<string> TestUnchangedMarkNames = new[]
    {
        "Yilan",
        "Stolas",
        "anything, honestly",
    };
    
    private readonly CiDataManager ciDataManager = new CiDataManager(@"Data\CustomNames.json");

    [Test]
    public void CorrectedMarkNames()
    {
        Assert.Multiple(() =>
        {
            TestCorrectedMarkNames.ForEach(correctedName =>
            {
                // DATA
                var expected = correctedName.Value;
                
                // WHEN
                var actual = ciDataManager.CorrectMarkName(correctedName.Key);
                
                // THEN
                Assert.That(actual, Is.EqualTo(expected));
            });
        });
    }

    [Test]
    public void UnchangedMarkNames()
    {
        Assert.Multiple(() =>
        {
            TestUnchangedMarkNames.ForEach(unchangedName =>
            {
                // DATA
                var expected = unchangedName;
                
                // WHEN
                var actual = ciDataManager.CorrectMarkName(unchangedName);
                
                // THEN
                Assert.That(actual, Is.EqualTo(expected));
            });
        });
    }

    [Test]
    public void CorrectingNamesIsCaseInsensitive()
    {
        // DATA
        var input = "ZANIGOH";
        var expected = "Zanig'oh";

        // WHEN
        var actual = ciDataManager.CorrectMarkName(input);

        // THEN
        Assert.That(actual, Is.EqualTo(expected));
    }
}
