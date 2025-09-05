HOW TO INTEGRATE (quick steps)
--------------------------------
1) Add these files to your project:
   - ChannelConfig.cs
   - Mirroring/IMirrorEndpoint.cs
   - Mirroring/MirrorPair.cs
   - AmppChannelMirrorApp.Mirroring.cs
   - Program_MirrorBootstrap.cs
   - GlobalUsings.cs  (optional alias CrashLiveApp -> AmppChannelMirrorApp)

2) Ensure your main class is renamed to AmppChannelMirrorApp OR keep the alias:
   - If you already renamed CrashLiveApp to AmppChannelMirrorApp, delete GlobalUsings.cs.
   - If not renamed yet, keep GlobalUsings.cs so old references compile.

3) Replace external TAKE/TAKENEXT calls with mirror-aware versions:
   - await app.MirrorAware_TakeAsync(sourceOrKey, awaitPeer: false);
   - await app.MirrorAware_TakeNextAsync(awaitPeer: false);

   These run local + peer operations in parallel (peer is fire-and-forget by default).
   Set awaitPeer = true if you want to wait for both sides before returning.

4) Start all pairs from Program.Main (after you load parameters and args[0] XML):
   await MirrorBootstrap.StartAllAsync(args[0], parameters, log);

5) Your Config_MirrorApp.xml must include:
   <Channels>
     <ChannelEntry NAME="CH01_MASTER" NETWORKID="..." PLATFORMURI="..." PLATFORMAPIKEY="..." />
     <ChannelEntry NAME="CH01_SLAVE"  NETWORKID="..." PLATFORMURI="..." PLATFORMAPIKEY="..." />
     ...
   </Channels>
   <MirrorMap>CH01_MASTER=CH01_SLAVE;CH02_MASTER=CH02_SLAVE</MirrorMap>

Notes:
- Internal methods (Internal_TakeAsync/TakeNextAsync) are parallelised via Task.Run to match your "threads" ask.
- The MirrorPair class creates independent AMPP connections per channel (URI/API key from each ChannelEntry).
- TCPSocketServer is still started per instance with unique ports.

