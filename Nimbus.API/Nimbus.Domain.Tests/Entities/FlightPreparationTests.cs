using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
namespace Nimbus.Domain.Tests.Entities;

/// <summary>
///     Domain-level shape tests for the flight preparation dossier and its children (issue #31).
///     No framework/persistence involved — see <c>Nimbus.Infrastructure.Tests</c> for migration
///     and cascade-delete coverage.
/// </summary>
[TestFixture]
public class FlightPreparationTests
{
    [Test]
    public void NewFlightPreparation_DefaultsToPlanned()
    {
        var flight = new FlightPreparation();

        Assert.That(flight.Status, Is.EqualTo(FlightPreparationStatus.Planned));
    }

    [Test]
    public void NewFlightPreparation_ChecklistsDefaultToUnchecked()
    {
        var flight = new FlightPreparation();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flight.FirstAidAndExtinguisher, Is.EqualTo(ChecklistAnswer.No));
            Assert.That(flight.PassengerListFilled, Is.EqualTo(ChecklistAnswer.No));
            Assert.That(flight.FlightPlanFiled, Is.EqualTo(ChecklistAnswer.No));
            Assert.That(flight.NotamsChecked, Is.False);
            Assert.That(flight.BurnerTested, Is.False);
            Assert.That(flight.GasCylindersChecked, Is.False);
            Assert.That(flight.BalloonVisualInspectionDone, Is.False);
            Assert.That(flight.MooringsChecked, Is.False);
            Assert.That(flight.InstrumentsChecked, Is.False);
        }
    }

    [Test]
    public void ChecklistAnswer_HasYesNoNotApplicable_WithNoAsDefaultValue()
    {
        Assert.That(Enum.GetValues<ChecklistAnswer>(), Is.EquivalentTo(new[] { ChecklistAnswer.No, ChecklistAnswer.Yes, ChecklistAnswer.NotApplicable }));
        Assert.That(default(ChecklistAnswer), Is.EqualTo(ChecklistAnswer.No));
    }

    [Test]
    public void NewFlightPreparation_ChildCollectionsStartEmpty()
    {
        var flight = new FlightPreparation();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flight.Passengers, Is.Empty);
            Assert.That(flight.Images, Is.Empty);
            Assert.That(flight.WindLevels, Is.Empty);
            Assert.That(flight.Shares, Is.Empty);
        }
    }

    [Test]
    public void NewFlightPreparation_HasNoFlightOutcomeFieldsUntilFlown()
    {
        var flight = new FlightPreparation();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flight.FlownAt, Is.Null);
            Assert.That(flight.ActualDurationMinutes, Is.Null);
            Assert.That(flight.KmlTrackObjectKey, Is.Null);
            Assert.That(flight.KmlTrackContentType, Is.Null);
        }
    }

    [Test]
    public void MarkingAsFlown_SetsOutcomeFieldsAndKmlTrackReference_NotBytes()
    {
        var flight = new FlightPreparation
        {
            Status = FlightPreparationStatus.Flown,
            FlownAt = new DateTime(2026, 8, 8, 19, 57, 0, DateTimeKind.Utc),
            ActualDurationMinutes = 72,
            ActualLandingLocation = "Weiland t.h.v. Ravelsestraat 14",
            // The KML track is an object-storage reference (key + content type), never file bytes.
            KmlTrackObjectKey = "00000000-0000-0000-0000-000000000001/00000000-0000-0000-0000-000000000002/track.kml",
            KmlTrackContentType = "application/vnd.google-earth.kml+xml"
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flight.Status, Is.EqualTo(FlightPreparationStatus.Flown));
            Assert.That(flight.KmlTrackObjectKey, Is.Not.Null.And.Not.Empty);
            Assert.That(flight.KmlTrackContentType, Is.EqualTo("application/vnd.google-earth.kml+xml"));
        }
    }

    [Test]
    public void FlightPreparationImage_StoresObjectReference_NotBytes()
    {
        var image = new FlightPreparationImage
        {
            FlightPreparationId = Guid.NewGuid(),
            Section = FlightPreparationSection.Meteo,
            ObjectKey = "owner/flight/photo.jpg",
            ContentType = "image/jpeg",
            Order = 0
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(image.ObjectKey, Is.Not.Null.And.Not.Empty);
            Assert.That(image.ContentType, Is.EqualTo("image/jpeg"));
            Assert.That(image.Section, Is.EqualTo(FlightPreparationSection.Meteo));
        }
    }

    [Test]
    public void FlightPreparationSection_HasNineValues_OneForEachDossierSection()
    {
        var sections = Enum.GetValues<FlightPreparationSection>();

        Assert.That(sections, Has.Length.EqualTo(9));
    }
}
