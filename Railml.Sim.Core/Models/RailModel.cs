#pragma warning disable
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Railml.Sim.Core.Models
{
    // Simplified RailML 2.5 Structure

    [XmlRoot(ElementName = "railml", Namespace = "http://www.railml.org/schemas/2013")]
    public class Railml
    {
        [XmlAttribute(AttributeName = "version")]
        public string Version { get; set; } = "2.5";

        [XmlElement(ElementName = "infrastructure")]
        public Infrastructure Infrastructure { get; set; }

        [XmlElement(ElementName = "infrastructureVisualizations")]
        public InfrastructureVisualizations InfrastructureVisualizations { get; set; }

        [XmlNamespaceDeclarations]
        public XmlSerializerNamespaces Namespaces { get; set; } = new XmlSerializerNamespaces();
    }


    public class Infrastructure
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlElement(ElementName = "routes", Namespace = "http://www.sehwa.co.kr/railml")]
        public Routes Routes { get; set; }

        [XmlElement(ElementName = "areas", Namespace = "http://www.sehwa.co.kr/railml")]
        public Areas Areas { get; set; }

        [XmlElement(ElementName = "tracks")]
        public Tracks Tracks { get; set; }
    }

    public class Tracks
    {
        [XmlElement(ElementName = "track")]
        public List<Track> TrackList { get; set; } = new List<Track>();
    }

    public class Track
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "description")]
        public string Description { get; set; }

        [XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }

        [XmlAttribute(AttributeName = "mainDir")]
        public string MainDir { get; set; }

        [XmlElement(ElementName = "trackTopology")]
        public TrackTopology TrackTopology { get; set; }

        [XmlElement(ElementName = "ocsElements")]
        public OcsElements OcsElements { get; set; }

        [XmlAttribute(AttributeName = "code")]
        public string Code { get; set; }
    }

    public class OcsElements
    {
        [XmlElement(ElementName = "signals")]
        public Signals Signals { get; set; }

        [XmlElement(ElementName = "trainDetectionElements")]
        public TrainDetectionElements TrainDetectionElements { get; set; }
    }

    public class TrainDetectionElements
    {
        [XmlElement(ElementName = "trackCircuitBorder")]
        public List<TrackCircuitBorder> TrackCircuitBorderList { get; set; } = new List<TrackCircuitBorder>();
    }

    public class TrackCircuitBorder
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "pos")]
        public double Pos { get; set; }

        [XmlAttribute(AttributeName = "code")]
        public string Code { get; set; }

        [XmlAttribute(AttributeName = "description")]
        public string Description { get; set; }
    }

    public class Signals
    {
        [XmlElement(ElementName = "signal")]
        public List<Signal> SignalList { get; set; } = new List<Signal>();
    }

    public class Signal
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "dir")]
        public string Dir { get; set; }

        [XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }

        [XmlAttribute(AttributeName = "function")]
        public string Function { get; set; }

        // Mapped to <additionalName name="...">
        [XmlElement(ElementName = "additionalName")]
        public AdditionalName AdditionalName { get; set; }

        [XmlAttribute(AttributeName = "pos")]
        public int Pos { get; set; }

        // Visual Coordinates (Legacy attributes, hide from Save but keep for Load)
        [XmlAttribute]
        public double X { get; set; }
        public bool ShouldSerializeX() => false;

        [XmlAttribute]
        public double Y { get; set; }
        public bool ShouldSerializeY() => false;

        // Legacy ScreenPos (Hide from Save)
        [XmlElement(ElementName = "screenPos", Namespace = "http://www.sehwa.co.kr/railml")]
        public ScreenPos ScreenPos { get; set; }
        public bool ShouldSerializeScreenPos() => false;
    }

    public class AdditionalName
    {
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
    }

    public class TrackTopology
    {
        [XmlElement(ElementName = "trackBegin")]
        public TrackNode TrackBegin { get; set; }

        [XmlElement(ElementName = "trackEnd")]
        public TrackNode TrackEnd { get; set; }

        [XmlElement(ElementName = "connections")]
        public Connections Connections { get; set; }

        // Legacy CornerPos (Hide from Save)
        [XmlElement(ElementName = "cornerPos", Namespace = "http://www.sehwa.co.kr/railml")]
        public CornerPos CornerPos { get; set; }
        public bool ShouldSerializeCornerPos() => false;
    }

    public class CornerPos
    {
        [XmlAttribute(AttributeName = "x")]
        public double X { get; set; }

        [XmlAttribute(AttributeName = "y")]
        public double Y { get; set; }
    }

    public class TrackNode
    {
        [XmlAttribute(AttributeName = "pos")]
        public double Pos { get; set; }

        [XmlAttribute(AttributeName = "absPos")]
        public double AbsPos { get; set; }

        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }
        
        [XmlElement(ElementName = "connection")]
        public List<Connection> ConnectionList { get; set; } = new List<Connection>();

        [XmlElement(ElementName = "bufferStop")]
        public BufferStop BufferStop { get; set; }

        [XmlElement(ElementName = "openEnd")]
        public OpenEnd OpenEnd { get; set; }

        // Legacy ScreenPos (Hide from Save)
        [XmlElement(ElementName = "screenPos", Namespace = "http://www.sehwa.co.kr/railml")]
        public ScreenPos ScreenPos { get; set; }
        public bool ShouldSerializeScreenPos() => false;
    }

    public class BufferStop
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "code")]
        public string Code { get; set; }
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
        [XmlAttribute(AttributeName = "description")]
        public string Description { get; set; }
    }

    public class OpenEnd
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "code")]
        public string Code { get; set; }
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
        [XmlAttribute(AttributeName = "description")]
        public string Description { get; set; }
    }

    public class Connection
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "ref")]
        public string Ref { get; set; }

        [XmlAttribute(AttributeName = "orientation")]
        public string Orientation { get; set; }

        [XmlAttribute(AttributeName = "course")]
        public string Course { get; set; }
    }

    public class Connections
    {
        [XmlElement(ElementName = "switch")]
        public List<Switch> Switches { get; set; } = new List<Switch>();

        [XmlElement(ElementName = "connection")]
        public List<Connection> ConnectionList { get; set; } = new List<Connection>();
    }

    public class Switch
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "pos")]
        public double Pos { get; set; }

        [XmlAttribute(AttributeName = "trackContinueCourse")]
        public string TrackContinueCourse { get; set; }

        [XmlAttribute(AttributeName = "normalPosition")]
        public string NormalPosition { get; set; }

        [XmlElement(ElementName = "screenPos", Namespace = "http://www.sehwa.co.kr/railml")]
        public ScreenPos ScreenPos { get; set; }
        public bool ShouldSerializeScreenPos() => false;

        [XmlElement(ElementName = "additionalName")]
        public AdditionalName AdditionalName { get; set; }

        [XmlElement(ElementName = "connection")]
        public List<Connection> ConnectionList { get; set; } = new List<Connection>();
    }


    public class Routes
    {
        [XmlElement(ElementName = "route", Namespace = "http://www.sehwa.co.kr/railml")]
        public List<Route> RouteList { get; set; } = new List<Route>();
    }

    public class Route
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "code")]
        public string Code { get; set; }

        [XmlAttribute(AttributeName = "description")]
        public string Description { get; set; }

        [XmlAttribute(AttributeName = "approachPointRef")]
        public string ApproachPointRef { get; set; }

        [XmlAttribute(AttributeName = "entryRef")]
        public string EntryRef { get; set; }

        [XmlAttribute(AttributeName = "exitRef")]
        public string ExitRef { get; set; }

        [XmlAttribute(AttributeName = "overlapEndRef")]
        public string OverlapEndRef { get; set; }

        [XmlAttribute(AttributeName = "proceedSpeed")]
        public string ProceedSpeed { get; set; }

        [XmlAttribute(AttributeName = "releaseTriggerHead")]
        public bool ReleaseTriggerHead { get; set; }

        [XmlIgnore]
        public bool ReleaseTriggerHeadSpecified { get; set; }

        [XmlAttribute(AttributeName = "releaseTriggerRef")]
        public string ReleaseTriggerRef { get; set; }

        [XmlElement(ElementName = "switchAndPosition", Namespace = "http://www.sehwa.co.kr/railml")]
        public List<SwitchAndPosition> SwitchAndPositionList { get; set; } = new List<SwitchAndPosition>();

        [XmlElement(ElementName = "overlapSwitchAndPosition")]
        public List<SwitchAndPosition> OverlapSwitchAndPositionList { get; set; } = new List<SwitchAndPosition>();

        [XmlElement(ElementName = "releaseGroup", Namespace = "http://www.sehwa.co.kr/railml")]
        public ReleaseGroup ReleaseGroup { get; set; }
    }

    public class ReleaseGroup
    {
        [XmlElement(ElementName = "trackSectionRef")]
        public List<TrackSectionRef> TrackSectionRefList { get; set; } = new List<TrackSectionRef>();
    }

    public class TrackSectionRef
    {
        [XmlAttribute(AttributeName = "ref")]
        public string Ref { get; set; }

        [XmlAttribute(AttributeName = "flankProtection")]
        public bool FlankProtection { get; set; }
    }

    public class Areas
    {
        [XmlElement(ElementName = "area", Namespace = "http://www.sehwa.co.kr/railml")]
        public List<Area> AreaList { get; set; } = new List<Area>();
    }

    public class Area
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "description")]
        public string Description { get; set; }

        [XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }

        [XmlElement(ElementName = "isLimitedBy", Namespace = "http://www.sehwa.co.kr/railml")]
        public List<IsLimitedBy> IsLimitedByList { get; set; } = new List<IsLimitedBy>();
    }

    public class IsLimitedBy
    {
        [XmlAttribute(AttributeName = "ref")]
        public string Ref { get; set; }
    }

    public class SwitchAndPosition
    {
        [XmlAttribute(AttributeName = "switchRef")]
        public string SwitchRef { get; set; }

        [XmlAttribute(AttributeName = "switchPosition")]
        public string SwitchPosition { get; set; }
    }


    public class ScreenPos
    {
        [XmlAttribute(AttributeName = "mx")]
        public double MX { get; set; }

        [XmlIgnore]
        public bool MXSpecified { get; set; }

        [XmlAttribute(AttributeName = "my")]
        public double MY { get; set; }

        [XmlIgnore]
        public bool MYSpecified { get; set; }

        [XmlAttribute(AttributeName = "x")]
        public double X { get; set; }

        [XmlIgnore]
        public bool XSpecified { get; set; }

        [XmlAttribute(AttributeName = "y")]
        public double Y { get; set; }

        [XmlIgnore]
        public bool YSpecified { get; set; }
    }


    // --- New Visualization Structure ---

    public class InfrastructureVisualizations
    {
        [XmlElement(ElementName = "visualization")]
        public List<Visualization> VisualizationList { get; set; } = new List<Visualization>();
    }

    public class Visualization
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "infrastructureRef")]
        public string InfrastructureRef { get; set; }

        [XmlElement(ElementName = "lineVis")]
        public List<LineVis> LineVisList { get; set; } = new List<LineVis>();

        [XmlElement(ElementName = "objectVis")]
        public List<ObjectVis> ObjectVisList { get; set; } = new List<ObjectVis>();
    }

    public class ObjectVis
    {
        [XmlAttribute(AttributeName = "ref")]
        public string Ref { get; set; }

        [XmlElement(ElementName = "position")]
        public VisualizationPosition Position { get; set; }
    }

    public class LineVis
    {
        [XmlElement(ElementName = "trackVis")]
        public List<TrackVis> TrackVisList { get; set; } = new List<TrackVis>();
    }

    public class TrackVis
    {
        [XmlAttribute(AttributeName = "ref")]
        public string Ref { get; set; }

        [XmlElement(ElementName = "trackElementVis")]
        public List<TrackElementVis> TrackElementVisList { get; set; } = new List<TrackElementVis>();
    }

    public class TrackElementVis
    {
        [XmlAttribute(AttributeName = "ref")]
        public string Ref { get; set; }

        [XmlElement(ElementName = "position")]
        public VisualizationPosition Position { get; set; }
    }

    public class VisualizationPosition
    {
        [XmlAttribute(AttributeName = "x")]
        public double X { get; set; }

        [XmlAttribute(AttributeName = "y")]
        public double Y { get; set; }
    }
}
