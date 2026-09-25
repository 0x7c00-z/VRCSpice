using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardCodec : UdonSharpBehaviour
{
    public string boardId = "board-01";
    public int maxJsonCharacters = 60000;
    public BreadboardState state;
    public BreadboardLayout layout;
    public BreadboardCatalog catalog;
    public BreadboardPlacement placement;
    [HideInInspector] public string error = "";
    [HideInInspector] public int decodedRevision, decodedNextSerial, decodedCount;
    private float decodedSupplyVoltage;
    private int[] ids, kinds, a, b, c, orientations, lengths, models;
    private float[] values;
    private bool decoded;

    public string Encode()
    {
        state.Initialize();
        DataDictionary root = new DataDictionary();
        root.Add("schemaVersion", 2); root.Add("boardId", boardId);
        root.Add("layoutId", layout.layoutId); root.Add("layoutVersion", layout.layoutVersion);
        root.Add("catalogId", catalog.catalogId); root.Add("catalogVersion", catalog.catalogVersion);
        root.Add("revision", state.revision); root.Add("nextComponentSerial", state.nextSerial);
        root.Add("supplyVoltage", state.supplyVoltage);
        DataList records = new DataList();
        for (int i = 0; i < state.count; i++)
        {
            int kind = state.kinds[i];
            DataDictionary record = new DataDictionary();
            record.Add("id", catalog.ComponentId(state.ids[i])); record.Add("kind", catalog.kinds[kind]);
            record.Add("footprintId", catalog.footprints[kind]); record.Add("orientation", state.orientations[i]);
            DataList pins = new DataList();
            pins.Add(layout.holeIds[state.pin0[i]]); pins.Add(layout.holeIds[state.pin1[i]]);
            if (kind == 5) pins.Add(layout.holeIds[state.pin2[i]]);
            record.Add("pins", pins);
            if (kind == 0) record.Add("lengthPitches", state.lengths[i]);
            else if (kind <= 3) record.Add("valueSI", state.values[i]);
            else record.Add("modelId", catalog.modelIds[state.models[i]]);
            records.Add(record);
        }
        root.Add("components", records);
        DataToken output;
        if (!VRCJson.TrySerializeToJson(root, JsonExportType.Minify, out output)) { error = "Cannot encode circuit"; return null; }
        if (output.String.Length > maxJsonCharacters) { error = "Circuit exceeds sync capacity"; return null; }
        error = "";
        return output.String;
    }

    private string ReadString(DataDictionary record, string key)
    {
        DataToken token;
        if (!record.TryGetValue(key, TokenType.String, out token)) return null;
        return token.String;
    }

    private int ReadInt(DataDictionary record, string key)
    {
        DataToken token;
        if (!record.TryGetValue(key, TokenType.Double, out token)) return -1;
        double number = token.Double;
        if (number < 0 || number > int.MaxValue || double.IsNaN(number)) return -1;
        int value = (int)number;
        return value == number ? value : -1;
    }

    public bool TryDecode(string json)
    {
        decoded = false; error = "Invalid circuit data";
        state.Initialize();
        if (string.IsNullOrEmpty(json) || json.Length > maxJsonCharacters) return false;
        DataToken token;
        if (!VRCJson.TryDeserializeFromJson(json, out token) || token.TokenType != TokenType.DataDictionary) return false;
        DataDictionary root = token.DataDictionary;
        int schema = ReadInt(root, "schemaVersion");
        if ((schema != 1 && schema != 2) || ReadString(root, "boardId") != boardId ||
            ReadString(root, "layoutId") != layout.layoutId || ReadInt(root, "layoutVersion") != layout.layoutVersion ||
            ReadString(root, "catalogId") != catalog.catalogId || ReadInt(root, "catalogVersion") != catalog.catalogVersion)
        { error = "Board or catalog version mismatch"; return false; }
        decodedSupplyVoltage = layout.supplyVoltage;
        if (schema == 2)
        {
            if (!root.TryGetValue("supplyVoltage", TokenType.Double, out token)) return false;
            double voltage = token.Double;
            if (double.IsNaN(voltage) || voltage < -1e12 || voltage > 1e12) return false;
            decodedSupplyVoltage = (float)voltage;
        }
        decodedRevision = ReadInt(root, "revision"); decodedNextSerial = ReadInt(root, "nextComponentSerial");
        if (decodedRevision < 0 || decodedNextSerial < 1) return false;
        if (!root.TryGetValue("components", TokenType.DataList, out token)) return false;
        DataList records = token.DataList;
        decodedCount = records.Count;
        if (decodedCount > state.capacity) { error = "Too many components"; return false; }
        ids = new int[decodedCount]; kinds = new int[decodedCount]; a = new int[decodedCount];
        b = new int[decodedCount]; c = new int[decodedCount]; orientations = new int[decodedCount];
        lengths = new int[decodedCount]; models = new int[decodedCount]; values = new float[decodedCount];
        bool[] occupied = new bool[layout.holeIds.Length];
        DataDictionary usedIds = new DataDictionary();
        for (int i = 0; i < decodedCount; i++)
        {
            if (!records.TryGetValue(i, TokenType.DataDictionary, out token)) return false;
            DataDictionary record = token.DataDictionary;
            string id = ReadString(record, "id");
            int serial;
            if (string.IsNullOrEmpty(id) || !id.StartsWith("c") || !int.TryParse(id.Substring(1), out serial)) return false;
            if (serial < 1 || serial >= decodedNextSerial || id != catalog.ComponentId(serial) || usedIds.ContainsKey(id)) return false;
            usedIds.Add(id, true); ids[i] = serial;
            int kind = catalog.KindIndex(ReadString(record, "kind"));
            if (kind < 0 || ReadString(record, "footprintId") != catalog.footprints[kind]) return false;
            kinds[i] = kind; orientations[i] = ReadInt(record, "orientation");
            if (orientations[i] < 0 || orientations[i] > 3) return false;
            models[i] = -1; lengths[i] = 0; values[i] = 0f;
            if (kind == 0)
            {
                if (record.ContainsKey("valueSI") || record.ContainsKey("modelId")) return false;
                lengths[i] = ReadInt(record, "lengthPitches");
            }
            else if (kind <= 3)
            {
                if (record.ContainsKey("modelId") || record.ContainsKey("lengthPitches")) return false;
                if (!record.TryGetValue("valueSI", TokenType.Double, out token)) return false;
                double number = token.Double;
                if (double.IsNaN(number) || number > float.MaxValue || number < -float.MaxValue) return false;
                values[i] = (float)number;
            }
            else
            {
                if (record.ContainsKey("valueSI") || record.ContainsKey("lengthPitches")) return false;
                models[i] = catalog.ModelIndex(ReadString(record, "modelId"), kind);
            }
            if (!catalog.ValidParameters(kind, values[i], models[i], lengths[i])) return false;
            if (!record.TryGetValue("pins", TokenType.DataList, out token)) return false;
            DataList pins = token.DataList;
            if (pins.Count != catalog.PinCount(kind)) return false;
            c[i] = -1;
            for (int p = 0; p < pins.Count; p++)
            {
                if (!pins.TryGetValue(p, TokenType.String, out token)) return false;
                int hole = layout.HoleIndex(token.String);
                if (hole < 0 || occupied[hole]) { error = "Unknown or occupied hole"; return false; }
                occupied[hole] = true;
                if (p == 0) a[i] = hole;
                else if (p == 1) b[i] = hole;
                else c[i] = hole;
            }
            if (!placement.Matches(kind, a[i], b[i], c[i], orientations[i], lengths[i]))
            { error = "Pin geometry mismatch"; return false; }
        }
        decoded = true; error = ""; return true;
    }

    public void ApplyDecoded()
    {
        if (!decoded) return;
        // Canonical ordering is independent of JSON array/key order.
        bool[] used = new bool[decodedCount];
        for (int dst = 0; dst < decodedCount; dst++)
        {
            int src = -1;
            for (int i = 0; i < decodedCount; i++)
                if (!used[i] && (src < 0 || ids[i] < ids[src])) src = i;
            used[src] = true;
            state.ids[dst] = ids[src]; state.kinds[dst] = kinds[src]; state.pin0[dst] = a[src];
            state.pin1[dst] = b[src]; state.pin2[dst] = c[src]; state.orientations[dst] = orientations[src];
            state.lengths[dst] = lengths[src]; state.models[dst] = models[src]; state.values[dst] = values[src];
        }
        state.count = decodedCount; state.revision = decodedRevision; state.nextSerial = decodedNextSerial;
        state.supplyVoltage = decodedSupplyVoltage;
        state.RebuildOccupancy(); decoded = false;
    }

    public bool DecodedMatchesState()
    {
        if (!decoded || decodedCount != state.count || decodedRevision != state.revision || decodedNextSerial != state.nextSerial || decodedSupplyVoltage != state.supplyVoltage) return false;
        for (int i = 0; i < decodedCount; i++)
        {
            int j = state.FindId(ids[i]);
            if (j < 0 || kinds[i] != state.kinds[j] || a[i] != state.pin0[j] || b[i] != state.pin1[j] ||
                c[i] != state.pin2[j] || orientations[i] != state.orientations[j] || lengths[i] != state.lengths[j]) return false;
            if (kinds[i] >= 1 && kinds[i] <= 3 && values[i] != state.values[j]) return false;
            if (kinds[i] >= 4 && models[i] != state.models[j]) return false;
        }
        return true;
    }
}
