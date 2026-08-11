using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class TiledMapPrototypeImporter
{
    public const string SourceAssetPath = "Assets/MapPrototype/Tiled120x90/map_120x90_v1.tmj";

    public sealed class Layout
    {
        public string SourceSha256;
        public Rect Bounds;
        public int TileWidthPixels;
        public int TileHeightPixels;
        public float WorldUnitsPerTile;
        public float ScatterClearance;
        public Vector2[] RiverPoints;
        public float RiverWidth;
        public float RiverCollectorMargin;
        public Lake Lake;
        public Region[] Regions;
        public MapObject[] Obstacles;
        public MapObject[] Decorations;
        public MapObject[] Landmarks;
        public Vector2 CameraStart;
        public Vector2 WindStart;
    }

    public sealed class Region
    {
        public int SourceId;
        public string Name;
        public string RegionId;
        public Rect Bounds;
        public float GreenBias;
    }

    public sealed class Lake
    {
        public int SourceId;
        public string Name;
        public Vector2 Position;
        public Vector2 Size;
        public float CollectorMargin;
    }

    public sealed class MapObject
    {
        public int SourceId;
        public string Name;
        public string PrefabKey;
        public Vector2 Position;
        public Vector2 Size;
        public float Rotation;
        public bool BlocksLeaf;
    }

    public static Layout LoadAndValidate()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, SourceAssetPath));
        if (!File.Exists(absolutePath)) throw new FileNotFoundException("Missing Tiled map prototype", SourceAssetPath);

        string json = File.ReadAllText(absolutePath, Encoding.UTF8);
        Dictionary<string, object> root = AsObject(MiniJson.Deserialize(json), "map root");

        int width = GetInt(root, "width");
        int height = GetInt(root, "height");
        int tileWidth = GetInt(root, "tilewidth");
        int tileHeight = GetInt(root, "tileheight");
        if (width != 120 || height != 90 || tileWidth != 32 || tileHeight != 32)
        {
            throw new InvalidDataException(
                $"Expected a 120x90 map with 32px tiles, got {width}x{height} with {tileWidth}x{tileHeight}px tiles.");
        }

        Dictionary<string, object> mapProperties = ReadProperties(root);
        float worldUnitsPerTile = GetFloat(mapProperties, "worldUnitsPerTile", 0f);
        string scatterMode = GetString(mapProperties, "scatterMode", string.Empty);
        if (!Mathf.Approximately(worldUnitsPerTile, 1f))
            throw new InvalidDataException("worldUnitsPerTile must be 1.0.");
        if (!string.Equals(scatterMode, "RandomWalkableArea", StringComparison.Ordinal))
            throw new InvalidDataException("scatterMode must be RandomWalkableArea.");
        if (!GetBool(mapProperties, "scatterAvoidWater", false)
            || !GetBool(mapProperties, "scatterAvoidObstacles", false))
            throw new InvalidDataException("The prototype must avoid both water and obstacles while scattering.");

        List<object> layerValues = GetList(root, "layers");
        Dictionary<string, Dictionary<string, object>> layers = new Dictionary<string, Dictionary<string, object>>();
        for (int i = 0; i < layerValues.Count; i++)
        {
            Dictionary<string, object> layer = AsObject(layerValues[i], $"layer {i}");
            string layerName = GetString(layer, "name", string.Empty);
            if (layerName.IndexOf("LeafSpawnArea", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidDataException("LeafSpawnArea layers are forbidden by the prototype schema.");

            if (layer.TryGetValue("objects", out object rawObjects) && rawObjects is List<object> tiledObjects)
            {
                for (int objectIndex = 0; objectIndex < tiledObjects.Count; objectIndex++)
                {
                    Dictionary<string, object> tiledObject = AsObject(tiledObjects[objectIndex], $"{layerName} object {objectIndex}");
                    string objectName = GetString(tiledObject, "name", string.Empty);
                    string objectType = GetString(tiledObject, "type", string.Empty);
                    if (objectName.IndexOf("LeafSpawnArea", StringComparison.OrdinalIgnoreCase) >= 0
                        || objectType.IndexOf("LeafSpawnArea", StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new InvalidDataException("LeafSpawnArea objects are forbidden by the prototype schema.");
                }
            }
            layers[layerName] = layer;
        }

        string[] requiredLayers =
        {
            "20_River", "25_Lakes", "40_Obstacles", "50_Decorations", "60_Landmarks", "70_GameplayPoints"
        };
        for (int i = 0; i < requiredLayers.Length; i++)
        {
            if (!layers.ContainsKey(requiredLayers[i]))
                throw new InvalidDataException($"Missing required Tiled layer: {requiredLayers[i]}");
        }

        Rect mapBounds = new Rect(-width * 0.5f, -height * 0.5f, width, height);
        Region[] regions = layers.TryGetValue("10_Regions", out Dictionary<string, object> regionsLayer)
            ? ParseRegions(regionsLayer, width, height, tileWidth, tileHeight)
            : Array.Empty<Region>();

        Dictionary<string, object> riverObject = GetOnlyObject(layers["20_River"], "river");
        List<object> polyline = GetList(riverObject, "polyline");
        if (polyline.Count < 2) throw new InvalidDataException("MainRiver must contain at least two polyline points.");
        float riverOriginX = GetFloat(riverObject, "x", 0f);
        float riverOriginY = GetFloat(riverObject, "y", 0f);
        Vector2[] riverPoints = new Vector2[polyline.Count];
        for (int i = 0; i < polyline.Count; i++)
        {
            Dictionary<string, object> point = AsObject(polyline[i], $"river point {i}");
            riverPoints[i] = ToWorldPoint(
                riverOriginX + GetFloat(point, "x", 0f),
                riverOriginY + GetFloat(point, "y", 0f),
                width,
                height,
                tileWidth,
                tileHeight);
        }

        Dictionary<string, object> riverProperties = ReadProperties(riverObject);
        float riverWidth = GetFloat(riverProperties, "widthM", 0f);
        if (!Mathf.Approximately(riverWidth, 8f)) throw new InvalidDataException("MainRiver widthM must be 8.");
        ValidateCleanupProperties(riverProperties, "MainRiver");

        Lake lake = ParseLake(GetOnlyObject(layers["25_Lakes"], "lake"), width, height, tileWidth, tileHeight);
        MapObject[] obstacles = ParseMapObjects(layers["40_Obstacles"], width, height, tileWidth, tileHeight, true);
        MapObject[] decorations = ParseMapObjects(layers["50_Decorations"], width, height, tileWidth, tileHeight, false);
        MapObject[] landmarks = ParseMapObjects(layers["60_Landmarks"], width, height, tileWidth, tileHeight, false);
        ReadGameplayPoints(
            layers["70_GameplayPoints"],
            width,
            height,
            tileWidth,
            tileHeight,
            out Vector2 cameraStart,
            out Vector2 windStart);

        if (obstacles.Length != 11 || decorations.Length != 10 || landmarks.Length != 2)
        {
            throw new InvalidDataException(
                $"Expected 11 obstacles, 10 decorations, and 2 landmarks; got {obstacles.Length}, {decorations.Length}, {landmarks.Length}.");
        }

        return new Layout
        {
            SourceSha256 = ComputeSha256(absolutePath),
            Bounds = mapBounds,
            TileWidthPixels = tileWidth,
            TileHeightPixels = tileHeight,
            WorldUnitsPerTile = worldUnitsPerTile,
            ScatterClearance = GetFloat(mapProperties, "scatterClearanceM", 1f),
            RiverPoints = riverPoints,
            RiverWidth = riverWidth,
            RiverCollectorMargin = GetFloat(riverProperties, "collectorMarginM", 1f),
            Lake = lake,
            Regions = regions,
            Obstacles = obstacles,
            Decorations = decorations,
            Landmarks = landmarks,
            CameraStart = cameraStart,
            WindStart = windStart
        };
    }

    private static Region[] ParseRegions(
        Dictionary<string, object> layer,
        int mapWidth,
        int mapHeight,
        int tileWidth,
        int tileHeight)
    {
        List<object> objects = GetList(layer, "objects");
        Region[] result = new Region[objects.Count];
        for (int i = 0; i < objects.Count; i++)
        {
            Dictionary<string, object> source = AsObject(objects[i], $"region {i}");
            Dictionary<string, object> properties = ReadProperties(source);
            string regionId = GetString(properties, "regionId", GetString(source, "name", $"Region_{i + 1}"));
            result[i] = new Region
            {
                SourceId = GetInt(source, "id"),
                Name = GetString(source, "name", regionId),
                RegionId = regionId,
                Bounds = ToWorldRect(source, mapWidth, mapHeight, tileWidth, tileHeight),
                GreenBias = ResolveGreenBias(regionId)
            };
        }

        return result;
    }

    private static Lake ParseLake(
        Dictionary<string, object> source,
        int mapWidth,
        int mapHeight,
        int tileWidth,
        int tileHeight)
    {
        if (!GetBool(source, "ellipse", false)) throw new InvalidDataException("SouthWetlandLake must be an ellipse.");
        Dictionary<string, object> properties = ReadProperties(source);
        ValidateCleanupProperties(properties, GetString(source, "name", "lake"));
        Rect rect = ToWorldRect(source, mapWidth, mapHeight, tileWidth, tileHeight);
        return new Lake
        {
            SourceId = GetInt(source, "id"),
            Name = GetString(source, "name", "SouthWetlandLake"),
            Position = rect.center,
            Size = rect.size,
            CollectorMargin = GetFloat(properties, "collectorMarginM", 1f)
        };
    }

    private static MapObject[] ParseMapObjects(
        Dictionary<string, object> layer,
        int mapWidth,
        int mapHeight,
        int tileWidth,
        int tileHeight,
        bool defaultBlocksLeaf)
    {
        List<object> objects = GetList(layer, "objects");
        MapObject[] result = new MapObject[objects.Count];
        for (int i = 0; i < objects.Count; i++)
        {
            Dictionary<string, object> source = AsObject(objects[i], $"placed object {i}");
            Dictionary<string, object> properties = ReadProperties(source);
            string name = GetString(source, "name", $"Object_{i + 1}");
            string prefabKey = GetString(properties, "prefabKey", string.Empty);
            if (string.IsNullOrEmpty(prefabKey)) throw new InvalidDataException($"{name} has no prefabKey.");
            Rect rect = ToWorldRect(source, mapWidth, mapHeight, tileWidth, tileHeight);
            result[i] = new MapObject
            {
                SourceId = GetInt(source, "id"),
                Name = name,
                PrefabKey = prefabKey,
                Position = rect.center,
                Size = rect.size,
                Rotation = -GetFloat(source, "rotation", 0f),
                BlocksLeaf = GetBool(properties, "blocksLeaf", defaultBlocksLeaf)
            };
        }

        return result;
    }

    private static void ReadGameplayPoints(
        Dictionary<string, object> layer,
        int mapWidth,
        int mapHeight,
        int tileWidth,
        int tileHeight,
        out Vector2 cameraStart,
        out Vector2 windStart)
    {
        bool foundCamera = false;
        bool foundWind = false;
        cameraStart = default;
        windStart = default;

        List<object> objects = GetList(layer, "objects");
        for (int i = 0; i < objects.Count; i++)
        {
            Dictionary<string, object> source = AsObject(objects[i], $"gameplay point {i}");
            string name = GetString(source, "name", string.Empty);
            Vector2 position = ToWorldPoint(
                GetFloat(source, "x", 0f),
                GetFloat(source, "y", 0f),
                mapWidth,
                mapHeight,
                tileWidth,
                tileHeight);

            if (name == "CameraStart")
            {
                cameraStart = position;
                foundCamera = true;
            }
            else if (name == "WindStart")
            {
                windStart = position;
                foundWind = true;
            }
        }

        if (!foundCamera || !foundWind) throw new InvalidDataException("CameraStart and WindStart points are required.");
    }

    private static Dictionary<string, object> GetOnlyObject(Dictionary<string, object> layer, string label)
    {
        List<object> objects = GetList(layer, "objects");
        if (objects.Count != 1) throw new InvalidDataException($"Expected exactly one {label} object, got {objects.Count}.");
        return AsObject(objects[0], label);
    }

    private static Rect ToWorldRect(
        Dictionary<string, object> source,
        int mapWidth,
        int mapHeight,
        int tileWidth,
        int tileHeight)
    {
        float x = GetFloat(source, "x", 0f);
        float y = GetFloat(source, "y", 0f);
        float width = GetFloat(source, "width", 0f);
        float height = GetFloat(source, "height", 0f);
        Vector2 center = ToWorldPoint(
            x + width * 0.5f,
            y + height * 0.5f,
            mapWidth,
            mapHeight,
            tileWidth,
            tileHeight);
        Vector2 size = new Vector2(width / tileWidth, height / tileHeight);
        return new Rect(center - size * 0.5f, size);
    }

    private static Vector2 ToWorldPoint(
        float pixelX,
        float pixelY,
        int mapWidth,
        int mapHeight,
        int tileWidth,
        int tileHeight)
    {
        return new Vector2(pixelX / tileWidth - mapWidth * 0.5f, mapHeight * 0.5f - pixelY / tileHeight);
    }

    private static float ResolveGreenBias(string regionId)
    {
        switch (regionId)
        {
            case "StartRiverbank": return 0.75f;
            case "Meadow": return -0.75f;
            case "Forest": return 0.35f;
            case "OldTreeWetland": return 0.25f;
            default: return 0f;
        }
    }

    private static void ValidateCleanupProperties(Dictionary<string, object> properties, string objectName)
    {
        if (!GetBool(properties, "isCleanupTarget", false)
            || !GetBool(properties, "acceptsLeaves", false)
            || !GetBool(properties, "acceptsDebris", false))
            throw new InvalidDataException($"{objectName} must be a cleanup target accepting leaves and debris.");
    }

    private static Dictionary<string, object> ReadProperties(Dictionary<string, object> source)
    {
        Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (!source.TryGetValue("properties", out object raw) || raw == null) return result;
        List<object> properties = raw as List<object>;
        if (properties == null) return result;

        for (int i = 0; i < properties.Count; i++)
        {
            Dictionary<string, object> property = AsObject(properties[i], $"property {i}");
            string name = GetString(property, "name", string.Empty);
            if (!string.IsNullOrEmpty(name) && property.TryGetValue("value", out object value)) result[name] = value;
        }

        return result;
    }

    private static string ComputeSha256(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            byte[] hash = sha.ComputeHash(stream);
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    private static Dictionary<string, object> AsObject(object value, string label)
    {
        if (value is Dictionary<string, object> result) return result;
        throw new InvalidDataException($"Expected JSON object for {label}.");
    }

    private static List<object> GetList(Dictionary<string, object> source, string key)
    {
        if (source.TryGetValue(key, out object value) && value is List<object> result) return result;
        throw new InvalidDataException($"Missing JSON array: {key}");
    }

    private static int GetInt(Dictionary<string, object> source, string key)
    {
        return Mathf.RoundToInt(GetFloat(source, key, float.NaN));
    }

    private static float GetFloat(Dictionary<string, object> source, string key, float fallback)
    {
        if (!source.TryGetValue(key, out object value) || value == null) return fallback;
        if (value is double number) return (float)number;
        if (value is long integer) return integer;
        if (float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)) return parsed;
        return fallback;
    }

    private static string GetString(Dictionary<string, object> source, string key, string fallback)
    {
        return source.TryGetValue(key, out object value) && value != null ? value.ToString() : fallback;
    }

    private static bool GetBool(Dictionary<string, object> source, string key, bool fallback)
    {
        if (!source.TryGetValue(key, out object value) || value == null) return fallback;
        if (value is bool boolean) return boolean;
        return bool.TryParse(value.ToString(), out bool parsed) ? parsed : fallback;
    }

    private static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            using (Parser parser = new Parser(json)) return parser.ParseValue();
        }

        private sealed class Parser : IDisposable
        {
            private readonly StringReader reader;

            public Parser(string json)
            {
                reader = new StringReader(json);
            }

            public void Dispose()
            {
                reader.Dispose();
            }

            public object ParseValue()
            {
                EatWhitespace();
                int peek = reader.Peek();
                if (peek < 0) return null;
                switch ((char)peek)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': ConsumeLiteral("true"); return true;
                    case 'f': ConsumeLiteral("false"); return false;
                    case 'n': ConsumeLiteral("null"); return null;
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
                reader.Read();
                while (true)
                {
                    EatWhitespace();
                    if (reader.Peek() == '}')
                    {
                        reader.Read();
                        return result;
                    }

                    string key = ParseString();
                    EatWhitespace();
                    if (reader.Read() != ':') throw new InvalidDataException("Invalid JSON object separator.");
                    result[key] = ParseValue();
                    EatWhitespace();
                    int separator = reader.Read();
                    if (separator == '}') return result;
                    if (separator != ',') throw new InvalidDataException("Invalid JSON object terminator.");
                }
            }

            private List<object> ParseArray()
            {
                List<object> result = new List<object>();
                reader.Read();
                while (true)
                {
                    EatWhitespace();
                    if (reader.Peek() == ']')
                    {
                        reader.Read();
                        return result;
                    }

                    result.Add(ParseValue());
                    EatWhitespace();
                    int separator = reader.Read();
                    if (separator == ']') return result;
                    if (separator != ',') throw new InvalidDataException("Invalid JSON array terminator.");
                }
            }

            private string ParseString()
            {
                EatWhitespace();
                if (reader.Read() != '"') throw new InvalidDataException("Invalid JSON string.");
                StringBuilder builder = new StringBuilder();
                while (true)
                {
                    int next = reader.Read();
                    if (next < 0) throw new EndOfStreamException("Unterminated JSON string.");
                    char character = (char)next;
                    if (character == '"') return builder.ToString();
                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }

                    char escape = (char)reader.Read();
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            char[] hex = new char[4];
                            if (reader.Read(hex, 0, 4) != 4) throw new EndOfStreamException("Invalid JSON unicode escape.");
                            builder.Append((char)Convert.ToInt32(new string(hex), 16));
                            break;
                        default: throw new InvalidDataException($"Invalid JSON escape: {escape}");
                    }
                }
            }

            private object ParseNumber()
            {
                StringBuilder builder = new StringBuilder();
                while (reader.Peek() >= 0)
                {
                    char character = (char)reader.Peek();
                    if (!(char.IsDigit(character) || character == '-' || character == '+' || character == '.' || character == 'e' || character == 'E')) break;
                    builder.Append((char)reader.Read());
                }

                string number = builder.ToString();
                if (number.Length == 0) throw new InvalidDataException("Invalid JSON value.");
                if (number.IndexOf('.') < 0 && number.IndexOf('e') < 0 && number.IndexOf('E') < 0
                    && long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
                    return integer;
                if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double floating)) return floating;
                throw new InvalidDataException($"Invalid JSON number: {number}");
            }

            private void ConsumeLiteral(string literal)
            {
                for (int i = 0; i < literal.Length; i++)
                {
                    if (reader.Read() != literal[i]) throw new InvalidDataException($"Invalid JSON literal: {literal}");
                }
            }

            private void EatWhitespace()
            {
                while (reader.Peek() >= 0 && char.IsWhiteSpace((char)reader.Peek())) reader.Read();
            }
        }
    }
}
