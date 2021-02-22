## Flight Recorder

A simple recorder to record and replay flight in Microsoft Flight Simulator.

![Screenshot](Assets/Screenshot.png)

### Downloads

You can get the tool from our GitHub Releases (https://github.com/nguyenquyhy/Flight-Recorder/releases) or FlightSim.to (https://flightsim.to/file/8163/flight-recorder).

### Features

- Record and replay in the sim
- Change replaying speed
  - NOTE: when speeding up, your computer might not be able to load scenery fast enough and that will negatively affect frame rate.
- Save recording into a file and load it on another computer
- Export the recorded data into CSV for further analysis
- Quickly jump to any place in your recording (you have to Pause Replay first)

Notes: The tool records this list of variables from SimConnect [Structs.cs](FlightRecorder.Client.SimConnectMSFS/Structs.cs) for each sim frame. Some of them are only for analysis and display and don't affect replay.

### Current Limitations and Other Notes

- When starting a replay, your aircraft might be teleported to a far away location which doesn't have loaded terrain. This means the ground can jump up/down really quickly and you might get a crash (not CTD) due to damaged landing gear. Disable crash detection might be a good idea if you frequently replay your flight.
- I don’t know a reliable way to tell if an engine is running or not, so this tool does not auto-start the engine (because it doesn’t know when). Hence, you should start recording/replaying when engine is already running (or the aircraft will move without a running engine) and stop recording before turning off the engine (not really a problem, but the tool might not shut the engine down for you). Not doing that won't prevent you from replaying or using any features, but replay can look like your aircraft is powered by magic ;).
- Replay looks weird when turning on the ground. I’m not so sure what is happening there yet.
- To prevent fighting with MSFS own calculation, the tool sends freeze command when you start replay and unfreeze when you stop replay. 
This means replaying might conflict with other tools leverating the same freeze feature (e.g. YourControl when you are not in control, other replay tools).

### Issues

Please report any issues or feature request in GitHub Issues https://github.com/nguyenquyhy/Flight-Recorder/issues. 