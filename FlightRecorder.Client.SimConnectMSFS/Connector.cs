using Microsoft.Extensions.Logging;
using Microsoft.FlightSimulator.SimConnect;
using System;

namespace FlightRecorder.Client.SimConnectMSFS
{
    public partial class Connector
    {
        private SimConnect simconnect = null;

        public event EventHandler<AircraftPositionUpdatedEventArgs> AircraftPositionUpdated;
        public event EventHandler Initialized;
        public event EventHandler<ConnectorErrorEventArgs> Error;
        public event EventHandler Closed;

        private readonly ILogger<Connector> logger;

        public Connector(ILogger<Connector> logger)
        {
            this.logger = logger;
        }

        public void Initialize(IntPtr Handle)
        {
            simconnect = new SimConnect("Flight Recorder", Handle, WM_USER_SIMCONNECT, null, 0);

            // listen to connect and quit msgs
            simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(Simconnect_OnRecvOpen);
            simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(Simconnect_OnRecvQuit);

            simconnect.OnRecvException += Simconnect_OnRecvException;
            simconnect.OnRecvEvent += Simconnect_OnRecvEvent;
            simconnect.OnRecvSimobjectData += Simconnect_OnRecvSimobjectData;
            RegisterAircraftPositionDefinition();
            RegisterAircraftPositionSetDefinition();

            //simconnect.SubscribeToSystemEvent(EVENTS.POSITION_CHANGED, "PositionChanged");

            simconnect.OnRecvSystemState += Simconnect_OnRecvSystemState;

            simconnect.MapClientEventToSimEvent(EVENTS.FREEZE_LATITUDE_LONGITUDE, "FREEZE_LATITUDE_LONGITUDE_SET");
            simconnect.MapClientEventToSimEvent(EVENTS.FREEZE_ALTITUDE, "FREEZE_ALTITUDE_SET");
            simconnect.MapClientEventToSimEvent(EVENTS.FREEZE_ATTITUDE, "FREEZE_ATTITUDE_SET");
            RegisterEvents();

            Initialized?.Invoke(this, new());
        }

        public void Pause()
        {
            simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, EVENTS.FREEZE_LATITUDE_LONGITUDE, 1, GROUPS.GENERIC, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
            simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, EVENTS.FREEZE_ALTITUDE, 1, GROUPS.GENERIC, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
            simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, EVENTS.FREEZE_ATTITUDE, 1, GROUPS.GENERIC, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
        }

        public void Unpause()
        {
            simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, EVENTS.FREEZE_LATITUDE_LONGITUDE, 0, GROUPS.GENERIC, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
            simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, EVENTS.FREEZE_ALTITUDE, 0, GROUPS.GENERIC, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
            simconnect.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, EVENTS.FREEZE_ATTITUDE, 0, GROUPS.GENERIC, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
        }

        public void Set(AircraftPositionSetStruct position)
        {
            simconnect.SetDataOnSimObject(DEFINITIONS.AircraftPositionSet, 0, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
        }

        private void ProcessAircraftPosition(AircraftPositionStruct position)
        {
            logger.LogTrace("Get Aircraft status");
            AircraftPositionUpdated?.Invoke(this, new AircraftPositionUpdatedEventArgs(position));
        }

        private void RequestDataOnConnected()
        {
            simconnect.RequestDataOnSimObject(
                DATA_REQUESTS.AIRCRAFT_POSITION, DEFINITIONS.AircraftPosition, 0,
                SIMCONNECT_PERIOD.SIM_FRAME,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0, 0, 0);
        }

        #region Facility

        // User-defined win32 event
        const int WM_USER_SIMCONNECT = 0x0402;

        // Simconnect client will send a win32 message when there is
        // a packet to process. ReceiveMessage must be called to
        // trigger the events. This model keeps simconnect processing on the main thread.
        public IntPtr HandleSimConnectEvents(int message, ref bool isHandled)
        {
            isHandled = false;

            switch (message)
            {
                case WM_USER_SIMCONNECT:
                    {
                        if (simconnect != null)
                        {
                            try
                            {
                                this.simconnect.ReceiveMessage();
                            }
                            catch (Exception ex)
                            {
                                RecoverFromError(ex);
                            }

                            isHandled = true;
                        }
                    }
                    break;

                default:
                    logger.LogTrace("Unknown message type: {message}", message);
                    break;
            }

            return IntPtr.Zero;
        }

        private void RecoverFromError(Exception exception)
        {
            // 0xC000014B: CTD
            // 0xC00000B0: Sim has exited
            logger.LogError(exception, "Cannot receive SimConnect message!");
            //CloseConnection();
            Closed?.Invoke(this, new());
        }

        void Simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            logger.LogInformation("Connected to Flight Simulator {applicationName}", data.szApplicationName);
            RequestDataOnConnected();
        }

        void Simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            logger.LogInformation("Flight Simulator has exited");
            Closed?.Invoke(this, new());
            //CloseConnection();
        }

        void Simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            var error = (SIMCONNECT_EXCEPTION)data.dwException;
            logger.LogError("SimConnect error received: {error}", error);

            Error?.Invoke(this, new ConnectorErrorEventArgs(error));
        }

        private void Simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            // Must be general SimObject information
            switch (data.dwRequestID)
            {
                case (uint)DATA_REQUESTS.AIRCRAFT_POSITION:
                    {
                        var position = data.dwData[0] as AircraftPositionStruct?;
                        if (position.HasValue)
                        {
                            ProcessAircraftPosition(position.Value);
                        }
                    }
                    break;
            }
        }

        void Simconnect_OnRecvEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
        {
            logger.LogDebug("OnRecvEvent dwID {dwID} uEventID {uEventID}", (SIMCONNECT_RECV_ID)data.dwID, data.uEventID);
        }

        private void Simconnect_OnRecvSystemState(SimConnect sender, SIMCONNECT_RECV_SYSTEM_STATE data)
        {
            logger.LogDebug("OnRecvSystemState dwRequestID {dwRequestID}", (DATA_REQUESTS)data.dwRequestID);
        }

        private void RegisterDataDefinition<T>(DEFINITIONS definition, params (string datumName, string unitsName, SIMCONNECT_DATATYPE datumType)[] data)
        {
            foreach (var (datumName, unitsName, datumType) in data)
            {
                simconnect.AddToDataDefinition(definition, datumName, unitsName, datumType, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            }
            simconnect.RegisterDataDefineStruct<T>(definition);
        }

        #endregion

        /// <summary>
        /// Auto-generated by ConnectorGenerator
        /// </summary>
        //private void RegisterAircraftPositionDefinition();

        /// <summary>
        /// Auto-generated by ConnectorGenerator
        /// </summary>
        //private void RegisterAircraftPositionSetDefinition();

        /// <summary>
        /// Auto-generated by ConnectorGenerator
        /// </summary>
        //private void RegisterEvents();

        /// <summary>
        /// Auto-generated by ConnectorGenerator
        /// </summary>
        //public void TriggerEvents(AircraftPositionStruct current, AircraftPositionStruct expected);
    }
}
