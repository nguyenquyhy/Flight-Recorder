using SharpKml.Base;
using SharpKml.Dom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics;

public class KmlExportLogic : IExportLogic
{
    public string GetFileName() => $"Export {DateTime.Now:yyyy-MM-dd-HH-mm}.kml";
    public string GetFileFilter() => "KML|*.kml";

    public async Task ExportAsync(string fileName, IEnumerable<AircraftPosition> records)
    {
        await Task.Run(() =>
        {
            var kml = new Kml
            {
                Feature = new Placemark
                {
                    Geometry = new LineString
                    {
                        Coordinates = new(records.Select(o => new Vector
                        {
                            Latitude = o.Latitude,
                            Longitude = o.Longitude,
                            Altitude = o.Altitude * 0.3048,
                        })),
                        AltitudeMode = AltitudeMode.Absolute
                    }
                }
            };

            using var fileStream = File.OpenWrite(fileName);
            var serializer = new Serializer();
            serializer.Serialize(kml, fileStream);
        });
    }
}
