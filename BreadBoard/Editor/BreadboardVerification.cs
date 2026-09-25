using UnityEngine;
using UnityEditor;
using UdonSharpEditor;
using VRC.SDK3.Data;

public static class BreadboardVerification
{
    static int checks;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new System.Exception("Breadboard check failed: " + message);
        checks++;
    }
    static void SyncField(BreadboardSync sync, string name, object value)
    {
        typeof(BreadboardSync).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(sync, value);
    }
    [MenuItem("Tools/Breadboard/Verify data pipeline")]
    public static void Run()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Run in Edit mode.");
        var go = new GameObject("Breadboard verification (temporary)"); checks = 0;
        try
        {
            var layout = go.AddUdonSharpComponent<BreadboardLayout>(); BreadboardBuilder.ConfigureLayout(layout);
            var catalog = go.AddUdonSharpComponent<BreadboardCatalog>();
            var placement = go.AddUdonSharpComponent<BreadboardPlacement>(); placement.layout = layout; placement.catalog = catalog;
            var state = go.AddUdonSharpComponent<BreadboardState>(); state.layout = layout; state.catalog = catalog; state.placement = placement;
            var codec = go.AddUdonSharpComponent<BreadboardCodec>(); codec.state = state; codec.layout = layout; codec.catalog = catalog; codec.placement = placement;
            var connectivity = go.AddUdonSharpComponent<BreadboardConnectivity>(); connectivity.state = state; connectivity.layout = layout;
            var adapter = go.AddUdonSharpComponent<BreadboardMnaAdapter>(); adapter.state = state; adapter.layout = layout; adapter.catalog = catalog; adapter.connectivity = connectivity;
            state.Initialize();
            Check(layout.holeIds.Length == 400, "400 modeled holes");
            var holes = new System.Collections.Generic.HashSet<string>(layout.holeIds);
            Check(holes.Count == 400, "unique hole IDs");
            for (int i = 0; i < 400; i++) Check(layout.HoleIndex(layout.holeIds[i]) == i && layout.HoleAt(layout.holePositions[i]) == i, "hole lookup " + i);
            connectivity.Rebuild();
            Check(connectivity.nodeNames[0] == connectivity.nodeNames[120], "A-E conductor");
            Check(connectivity.nodeNames[0] != connectivity.nodeNames[150], "central gap");
            Check(connectivity.nodeNames[300] == connectivity.nodeNames[350] && connectivity.nodeNames[325] == "GND", "fixed rail feeds");
            Check(state.Add(0,0,2,1,0,-1), "wire row 1 to 2");
            Check(state.Add(0,31,2,1,0,-1), "wire row 2 to 3");
            connectivity.Rebuild(); Check(connectivity.nodeNames[0] == connectivity.nodeNames[2], "transitive wire union");
            Check(!state.Add(1,0,2,0,1000,-1) && state.count == 2, "atomic occupancy rejection");
            Check(!state.Add(5,29,2,0,0,2) && state.count == 2, "three-pin edge rejection");
            Check(state.Remove(1), "remove wire"); connectivity.Rebuild();
            Check(connectivity.nodeNames[0] != connectivity.nodeNames[2], "rebuild splits deleted connection");
            Check(state.Add(1,60,2,0,1000,-1), "resistor placement"); connectivity.Rebuild();
            Check(connectivity.nodeNames[60] != connectivity.nodeNames[63], "resistor does not merge nodes");
            string json = codec.Encode(); int revision = state.revision;
            Check(codec.TryDecode(json) && codec.DecodedMatchesState(), "JSON round trip");
            Check(!codec.TryDecode(json.Substring(0,json.Length/2)) && state.revision == revision && state.count == 2, "truncated JSON rollback");
            Check(!codec.TryDecode(json.Replace("A:1", "missing-hole")), "unknown hole");
            Check(!codec.TryDecode(json.Replace("\"kind\":\"R\"", "\"kind\":\"V\"")), "no placeable voltage source");
            Check(!codec.TryDecode(json.Replace("\"schemaVersion\":2", "\"schemaVersion\":99")), "schema mismatch");
            Check(codec.TryDecode(json), "valid data after invalid candidate"); codec.ApplyDecoded();
            Check(codec.Encode() == json, "canonical encode after apply");
            var netlist = adapter.BuildNetlist();
            Check(netlist.Count == 2 && netlist[0].DataList[0].String == "V_board_main", "one built-in source, no wire element");
            Check(netlist[0].DataList[2].DataList[0].TokenType == TokenType.Float, "MNA float constants");
            Check(catalog.MnaConstants(5,0,2).Count == 0, "Q future model boundary preserves existing solver contract");
            Check(catalog.MnaConstants(4,0,0)[0].Float != catalog.MnaConstants(4,0,1)[0].Float, "diode models resolve constants");
            Check(state.Add(0,300,3,1,0,-1), "shorted rails accepted as editable data");
            netlist = adapter.BuildNetlist();
            Check(netlist[0].DataList[1].DataList[0].String == "GND", "electrical validity delegated to solver");
            state.capacity = state.count;
            Check(!state.Add(1,90,2,0,1000,-1), "capacity guard");
            Check(placement.WireBetween(0,150) && placement.wireLength == 7 && placement.wireOrientation == 3, "two-point wire across gap");
            Check(!placement.WireBetween(0,151) && !placement.WireBetween(0,0), "wire diagonal and identical endpoints rejected");
            Check(placement.HandOrientation(Vector3.forward,0) == 1, "hand rotation quarter turn");
            Check(placement.HandOrientation(new Vector3(.71f,0,.70f),0) == 0, "rotation boundary hysteresis");
            Check(placement.HandOrientation(new Vector3(.5f,0,.86f),0) == 1, "rotation leaves hysteresis band");
            Check(placement.HandOrientation(Vector3.up,2) == 2, "near-normal hand axis preserves orientation");
            for (int kind = 1; kind <= 3; kind++)
            {
                for (int exponent = -11; exponent <= 10; exponent++)
                {
                    float decade = Mathf.Pow(10f,exponent);
                    float up = catalog.NextValue(kind,decade,1);
                    float down = catalog.NextValue(kind,decade,-1);
                    Check(Mathf.Abs(up/decade-1.1f)<.0001f, "E24 decade increase");
                    Check(Mathf.Abs(down/decade-.91f)<.0001f, "E24 decade decrease");
                }
                for (int step = 0; step < 23; step++)
                    Check(Mathf.Abs(catalog.NextValue(kind,catalog.e24[step],1)-catalog.e24[step+1])<.001f, "all E24 mantissas");
            }
            Check(catalog.NextValue(1,1e12f,1)==1e12f && catalog.NextValue(2,1e-12f,-1)==1e-12f, "E24 bounds do not wrap");
            Check(Mathf.Abs(catalog.NextValue(1,1050f,1)-1100f)<.01f && Mathf.Abs(catalog.NextValue(1,1050f,-1)-1000f)<.01f, "legacy off-series values step directionally");
            int beforeSupplyRevision = state.revision;
            Check(state.ChangeSupply(9f) && state.revision == beforeSupplyRevision+1, "supply is a revisioned edit");
            string voltageJson = codec.Encode();
            Check(codec.TryDecode(voltageJson) && codec.DecodedMatchesState(), "supply JSON round trip");
            Check(adapter.BuildNetlist()[0].DataList[2].DataList[0].Float == 9f, "edited supply reaches MNA");
            Check(!state.ChangeSupply(float.PositiveInfinity) && state.supplyVoltage == 9f, "nonfinite voltage preserves state");
            DataToken parsed; VRCJson.TryDeserializeFromJson(voltageJson,out parsed);
            parsed.DataDictionary.Remove("supplyVoltage");
            DataToken encoded; VRCJson.TrySerializeToJson(parsed,JsonExportType.Minify,out encoded);
            Check(!codec.TryDecode(encoded.String) && state.supplyVoltage == 9f, "schema 2 requires supply");
            parsed.DataDictionary["schemaVersion"] = 1;
            VRCJson.TrySerializeToJson(parsed,JsonExportType.Minify,out encoded);
            Check(codec.TryDecode(encoded.String), "legacy schema is readable"); codec.ApplyDecoded();
            Check(state.supplyVoltage == layout.supplyVoltage, "legacy supply uses board default");
            var probes = go.AddUdonSharpComponent<BreadboardProbes>();
            probes.layout = layout; probes.state = state; probes.connectivity = connectivity; probes.adapter = adapter;
            var input1 = new GameObject("CH1 test",typeof(RectTransform),typeof(TMPro.TMP_InputField)); input1.transform.SetParent(go.transform);
            var input2 = new GameObject("CH2 test",typeof(RectTransform),typeof(TMPro.TMP_InputField)); input2.transform.SetParent(go.transform);
            probes.channel1Input = input1.GetComponent<TMPro.TMP_InputField>(); probes.channel2Input = input2.GetComponent<TMPro.TMP_InputField>();
            probes.marker1 = new GameObject("Marker1 test").transform; probes.marker1.SetParent(go.transform);
            probes.marker2 = new GameObject("Marker2 test").transform; probes.marker2.SetParent(go.transform);
            connectivity.Rebuild(); adapter.appliedRevision = state.revision;
            int probeRevision = state.revision, probeCount = state.count;
            Check(probes.Select(0,31) && probes.Select(1,325), "select two probe holes");
            Check(probes.channel1Hole == "B:2" && probes.channel1Input.text == "N_A_1", "probe resolves through merged wire");
            Check(probes.channel2Input.text == "GND", "probe passes GND label to scope");
            Check(state.revision == probeRevision && state.count == probeCount, "probe selection does not edit circuit");
            Check(!probes.Select(0,-1) && probes.channel1Hole == "B:2", "invalid probe selection preserves point");
            state.Remove(0); probes.Resolve();
            Check(probes.channel1Input.text == "", "pending MNA update clears stale probe node");
            connectivity.Rebuild(); adapter.appliedRevision = state.revision; probes.Resolve();
            Check(probes.channel1Hole == "B:2" && probes.channel1Input.text == "N_A_2", "probe follows same hole after wire split");
            probes.Clear(0);
            Check(probes.channel1Input.text == "" && !probes.marker1.gameObject.activeSelf && probes.channel2Input.text == "GND", "clear one channel preserves other");
            // Exercise the actual transfer gate with delayed/replayed acknowledgements.
            // This is deterministic protocol validation, not a multi-client transport test.
            var sync = go.AddUdonSharpComponent<BreadboardSync>(); sync.state = state; sync.codec = codec;
            sync.hasState = true;
            int handoffRevision = state.revision;
            SyncField(sync, "pendingSnapshot", codec.Encode());
            SyncField(sync, "pendingGrant", 2); SyncField(sync, "pendingGrantRevision", handoffRevision);
            SyncField(sync, "pendingToken", 10); sync.dirty = true;
            sync.OnPreSerialization();
            Check(sync.grantedPlayerId == 2 && sync.grantedRevision == handoffRevision && sync.grantToken == 10, "grant and complete snapshot sent together");
            Check(!sync.MatchesAcknowledgement(2, handoffRevision, 10), "ACK before send completion cannot transfer");
            sync.lastSentRevision = handoffRevision; SyncField(sync, "lastSentGrant", 2); SyncField(sync, "lastSentToken", 10);
            Check(!sync.MatchesAcknowledgement(2, handoffRevision, 10), "failed or pending send remains blocked");
            sync.dirty = false;
            Check(sync.MatchesAcknowledgement(2, handoffRevision, 10), "matching received snapshot permits handoff");
            Check(!sync.MatchesAcknowledgement(3, handoffRevision, 10), "other player cannot acknowledge offer");
            Check(!sync.MatchesAcknowledgement(2, handoffRevision - 1, 10), "older circuit ACK rejected");
            Check(!sync.MatchesAcknowledgement(2, handoffRevision, 9), "previous handoff ACK rejected");
            state.revision++;
            Check(!sync.MatchesAcknowledgement(2, handoffRevision, 10), "new edit invalidates earlier offer");
            state.revision = handoffRevision;
            SyncField(sync, "pendingGrant", 1); SyncField(sync, "pendingToken", 11);
            Check(!sync.MatchesAcknowledgement(2, handoffRevision, 10), "pickup returned to first player invalidates second player ACK");
            SyncField(sync, "pendingGrant", 2); SyncField(sync, "pendingToken", 12);
            SyncField(sync, "lastSentToken", 12);
            Check(!sync.MatchesAcknowledgement(2, handoffRevision, 10), "same revision and same player on later handoff cannot reuse ACK");
            Check(sync.MatchesAcknowledgement(2, handoffRevision, 12), "later handoff accepts its own ACK");
            sync.hasState = false;
            Check(!sync.MatchesAcknowledgement(2, handoffRevision, 12), "missing initial state blocks handoff");
            Debug.Log("Breadboard verification PASSED: " + checks + " assertions.");
        }
        finally { Object.DestroyImmediate(go); }
    }
}
