namespace Fusion.Statistics {
  using System.Collections.Generic;
  using UnityEngine;

  public class FusionStatisticsNetworkPage : FusionStatisticsPage {
    /// <inheritdoc />
    public override string PageName => "Network";
    
    [Header("References")]
    [SerializeField] private LineChart _rtt;
    [SerializeField] private LineChart _inBandwidth;
    [SerializeField] private LineChart _outBandwidth;
    [SerializeField] private LineChart _inPackets;
    [SerializeField] private LineChart _outPackets;
    [SerializeField] private LineChart _inputInBandwidth;
    [SerializeField] private LineChart _inputOutBandwidth;

    private float _lastRTT; // used instead of 0.

    /// <inheritdoc />
    public override void Init() {
      var byteLabel = "{0} B";
      var unitTable = FusionStatsLookup.LOOKUP_TABLE_0;
      var byteTable = FusionStatsLookup.LOOKUP_TABLE_0_BYTES;

      _rtt.Setup("RTT", FusionStatsLookup.LOOKUP_TABLE_0ms, "{0} ms", forcePerUpdate: true);
      _inBandwidth.Setup("In Bandwidth", byteTable, byteLabel);
      _outBandwidth.Setup("Out Bandwidth", byteTable, byteLabel);
      _inPackets.Setup("In Packets", unitTable);
      _outPackets.Setup("Out Packets", unitTable);
      _inputInBandwidth.Setup("Input In Bandwidth", byteTable, byteLabel);
      _inputOutBandwidth.Setup("Input Out Bandwidth", byteTable, byteLabel);
    }

    /// <inheritdoc />
    public override void Render() {
      _rtt.RefreshDisplay();
      _inBandwidth.RefreshDisplay();
      _outBandwidth.RefreshDisplay();
      _inPackets.RefreshDisplay();
      _outPackets.RefreshDisplay();
      _inputInBandwidth.RefreshDisplay();
      _inputOutBandwidth.RefreshDisplay();
    }

    /// <inheritdoc />
    public override void AfterFusionUpdate() {
      var rtt = StatisticsManager.SimulationSnapshot.Stats.GetValueOrDefault(FusionStatType.RoundTripTime, 0);
      var inB = StatisticsManager.SimulationSnapshot.Stats.GetValueOrDefault(FusionStatType.InBandwidth, 0);
      var outB = StatisticsManager.SimulationSnapshot.Stats.GetValueOrDefault(FusionStatType.OutBandwidth, 0);
      var inP = StatisticsManager.SimulationSnapshot.Stats.GetValueOrDefault(FusionStatType.InPackets, 0);
      var outP = StatisticsManager.SimulationSnapshot.Stats.GetValueOrDefault(FusionStatType.OutPackets, 0);
      var inInput = StatisticsManager.SimulationSnapshot.Stats.GetValueOrDefault(FusionStatType.InputInBandwidth, 0);
      var outInput = StatisticsManager.SimulationSnapshot.Stats.GetValueOrDefault(FusionStatType.InputOutBandwidth, 0);

      if (rtt == 0) {
        rtt = _lastRTT;
      }

      _lastRTT =  rtt;
      rtt      *= 1000; // rtt is in seconds, convert to ms.

      _rtt.AddValue(rtt);
      _inBandwidth.AddValue(inB);
      _outBandwidth.AddValue(outB);
      _inPackets.AddValue(inP);
      _outPackets.AddValue(outP);
      _inputInBandwidth.AddValue(inInput);
      _inputOutBandwidth.AddValue(outInput);
    }
  }
}