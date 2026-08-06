// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// Two shapes started touching.
/// </summary>
public readonly record struct ContactBeginEvent
{
    /// <summary>Gets the first shape.</summary>
    public Shape ShapeA { get; init; }

    /// <summary>Gets the second shape.</summary>
    public Shape ShapeB { get; init; }
}

/// <summary>
/// Two shapes stopped touching.
/// </summary>
/// <remarks>
/// Also raised when a contact is destroyed for another reason, such as a shape
/// being removed or a filter changing, so either shape may already be gone.
/// Check <see cref="Shape.IsValid"/> before using them.
/// </remarks>
public readonly record struct ContactEndEvent
{
    /// <summary>Gets the first shape, which may have been destroyed.</summary>
    public Shape ShapeA { get; init; }

    /// <summary>Gets the second shape, which may have been destroyed.</summary>
    public Shape ShapeB { get; init; }
}

/// <summary>
/// Two shapes collided fast enough to be worth reporting.
/// </summary>
/// <remarks>
/// Raised only for shapes with hit events enabled, and only above the world's
/// hit threshold. This is what impact sounds and damage should listen to.
/// </remarks>
public readonly record struct ContactHitEvent
{
    /// <summary>Gets the first shape.</summary>
    public Shape ShapeA { get; init; }

    /// <summary>Gets the second shape.</summary>
    public Shape ShapeB { get; init; }

    /// <summary>Gets the point of impact, midway between the two surfaces.</summary>
    public Vector3 Point { get; init; }

    /// <summary>Gets the normal, pointing from the first shape to the second.</summary>
    public Vector3 Normal { get; init; }

    /// <summary>Gets how fast the shapes were approaching, usually in metres per second.</summary>
    /// <remarks>Always positive. Scale impact volume or damage by this.</remarks>
    public float ApproachSpeed { get; init; }

    /// <summary>Gets the user material identifier of the first shape.</summary>
    public ulong UserMaterialIdA { get; init; }

    /// <summary>Gets the user material identifier of the second shape.</summary>
    public ulong UserMaterialIdB { get; init; }
}

/// <summary>
/// A shape started overlapping a sensor.
/// </summary>
public readonly record struct SensorBeginEvent
{
    /// <summary>Gets the sensor shape.</summary>
    public Shape Sensor { get; init; }

    /// <summary>Gets the shape that entered the sensor.</summary>
    public Shape Visitor { get; init; }
}

/// <summary>
/// A shape stopped overlapping a sensor.
/// </summary>
/// <remarks>Either shape may already have been destroyed; check <see cref="Shape.IsValid"/>.</remarks>
public readonly record struct SensorEndEvent
{
    /// <summary>Gets the sensor shape, which may have been destroyed.</summary>
    public Shape Sensor { get; init; }

    /// <summary>Gets the shape that left the sensor, which may have been destroyed.</summary>
    public Shape Visitor { get; init; }
}

/// <summary>
/// A body was moved by the simulation.
/// </summary>
/// <remarks>
/// Only raised for bodies the solver moved, not for ones the application moved
/// itself. Walking this list is the efficient way to push transforms into game
/// objects: it arrives as one contiguous array containing only what changed,
/// instead of asking every body for its transform.
/// </remarks>
public readonly record struct BodyMoveEvent
{
    /// <summary>Gets the body that moved.</summary>
    public Body Body { get; init; }

    /// <summary>Gets the new world position of the body origin.</summary>
    public Vector3 Position { get; init; }

    /// <summary>Gets the new world rotation.</summary>
    public Quaternion Rotation { get; init; }

    /// <summary>Gets a value indicating whether the body fell asleep during this step.</summary>
    /// <remarks>
    /// When false the body is still awake, so the matching game object can be
    /// treated as active.
    /// </remarks>
    public bool FellAsleep { get; init; }
}

/// <summary>
/// The events raised during the most recent step.
/// </summary>
/// <remarks>
/// <para>
/// Every collection here is a view over engine memory rather than a copy, so
/// reading them allocates nothing. They stay valid only until the next
/// <see cref="PhysicsWorld.Step"/>.
/// </para>
/// <para>
/// It is safe to create and destroy bodies while walking these, which is the
/// whole reason Box3D buffers events instead of calling back mid-step. Be aware
/// that doing so can invalidate handles carried by events you have not read yet.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Step(1.0f / 60.0f);
///
/// foreach (var hit in world.Events.ContactHits)
/// {
///     PlayImpactSound(hit.Point, volume: hit.ApproachSpeed / 20.0f);
/// }
///
/// foreach (var moved in world.Events.BodyMoves)
/// {
///     transforms[moved.Body].Set(moved.Position, moved.Rotation);
/// }
/// </code>
/// </example>
public readonly ref struct WorldEvents
{
    private readonly b3ContactEvents _contacts;
    private readonly b3SensorEvents _sensors;
    private readonly b3BodyEvents _bodies;

    internal WorldEvents(b3WorldId world)
    {
        _contacts = B3.b3World_GetContactEvents(world);
        _sensors = B3.b3World_GetSensorEvents(world);
        _bodies = B3.b3World_GetBodyEvents(world);
    }

    /// <summary>Gets the pairs of shapes that started touching.</summary>
    public ContactBeginEventList ContactBegins => new(_contacts);

    /// <summary>Gets the pairs of shapes that stopped touching.</summary>
    public ContactEndEventList ContactEnds => new(_contacts);

    /// <summary>Gets the collisions fast enough to raise a hit event.</summary>
    public ContactHitEventList ContactHits => new(_contacts);

    /// <summary>Gets the shapes that entered a sensor.</summary>
    public SensorBeginEventList SensorBegins => new(_sensors);

    /// <summary>Gets the shapes that left a sensor.</summary>
    public SensorEndEventList SensorEnds => new(_sensors);

    /// <summary>Gets the bodies the simulation moved.</summary>
    public BodyMoveEventList BodyMoves => new(_bodies);

    /*
     * Each collection below is a thin struct enumerator over the native array.
     * Converting an event to its managed shape happens on read, one at a time,
     * so nothing is copied up front and a foreach over these allocates nothing
     * and boxes nothing.
     */

    /// <summary>A read-only view over the begin-touch events.</summary>
    public readonly ref struct ContactBeginEventList
    {
        private readonly b3ContactEvents _events;

        internal ContactBeginEventList(b3ContactEvents events) => _events = events;

        /// <summary>Gets the number of events.</summary>
        public int Count => _events.beginCount;

        /// <summary>Gets an enumerator over the events.</summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator() => new(_events);

        /// <summary>Walks the begin-touch events.</summary>
        public ref struct Enumerator
        {
            private readonly b3ContactEvents _events;
            private int _index;

            internal Enumerator(b3ContactEvents events)
            {
                _events = events;
                _index = -1;
            }

            /// <summary>Gets the event at the current position.</summary>
            public readonly unsafe ContactBeginEvent Current
            {
                get
                {
                    ref readonly b3ContactBeginTouchEvent e = ref _events.beginEvents[_index];
                    return new ContactBeginEvent
                    {
                        ShapeA = new Shape(e.shapeIdA),
                        ShapeB = new Shape(e.shapeIdB),
                    };
                }
            }

            /// <summary>Advances to the next event.</summary>
            /// <returns><see langword="true"/> while there are events remaining.</returns>
            public bool MoveNext() => ++_index < _events.beginCount;
        }
    }

    /// <summary>A read-only view over the end-touch events.</summary>
    public readonly ref struct ContactEndEventList
    {
        private readonly b3ContactEvents _events;

        internal ContactEndEventList(b3ContactEvents events) => _events = events;

        /// <summary>Gets the number of events.</summary>
        public int Count => _events.endCount;

        /// <summary>Gets an enumerator over the events.</summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator() => new(_events);

        /// <summary>Walks the end-touch events.</summary>
        public ref struct Enumerator
        {
            private readonly b3ContactEvents _events;
            private int _index;

            internal Enumerator(b3ContactEvents events)
            {
                _events = events;
                _index = -1;
            }

            /// <summary>Gets the event at the current position.</summary>
            public readonly unsafe ContactEndEvent Current
            {
                get
                {
                    ref readonly b3ContactEndTouchEvent e = ref _events.endEvents[_index];
                    return new ContactEndEvent
                    {
                        ShapeA = new Shape(e.shapeIdA),
                        ShapeB = new Shape(e.shapeIdB),
                    };
                }
            }

            /// <summary>Advances to the next event.</summary>
            /// <returns><see langword="true"/> while there are events remaining.</returns>
            public bool MoveNext() => ++_index < _events.endCount;
        }
    }

    /// <summary>A read-only view over the hit events.</summary>
    public readonly ref struct ContactHitEventList
    {
        private readonly b3ContactEvents _events;

        internal ContactHitEventList(b3ContactEvents events) => _events = events;

        /// <summary>Gets the number of events.</summary>
        public int Count => _events.hitCount;

        /// <summary>Gets an enumerator over the events.</summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator() => new(_events);

        /// <summary>Walks the hit events.</summary>
        public ref struct Enumerator
        {
            private readonly b3ContactEvents _events;
            private int _index;

            internal Enumerator(b3ContactEvents events)
            {
                _events = events;
                _index = -1;
            }

            /// <summary>Gets the event at the current position.</summary>
            public readonly unsafe ContactHitEvent Current
            {
                get
                {
                    ref readonly b3ContactHitEvent e = ref _events.hitEvents[_index];
                    return new ContactHitEvent
                    {
                        ShapeA = new Shape(e.shapeIdA),
                        ShapeB = new Shape(e.shapeIdB),
                        Point = e.point,
                        Normal = e.normal,
                        ApproachSpeed = e.approachSpeed,
                        UserMaterialIdA = e.userMaterialIdA,
                        UserMaterialIdB = e.userMaterialIdB,
                    };
                }
            }

            /// <summary>Advances to the next event.</summary>
            /// <returns><see langword="true"/> while there are events remaining.</returns>
            public bool MoveNext() => ++_index < _events.hitCount;
        }
    }

    /// <summary>A read-only view over the sensor begin-overlap events.</summary>
    public readonly ref struct SensorBeginEventList
    {
        private readonly b3SensorEvents _events;

        internal SensorBeginEventList(b3SensorEvents events) => _events = events;

        /// <summary>Gets the number of events.</summary>
        public int Count => _events.beginCount;

        /// <summary>Gets an enumerator over the events.</summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator() => new(_events);

        /// <summary>Walks the sensor begin-overlap events.</summary>
        public ref struct Enumerator
        {
            private readonly b3SensorEvents _events;
            private int _index;

            internal Enumerator(b3SensorEvents events)
            {
                _events = events;
                _index = -1;
            }

            /// <summary>Gets the event at the current position.</summary>
            public readonly unsafe SensorBeginEvent Current
            {
                get
                {
                    ref readonly b3SensorBeginTouchEvent e = ref _events.beginEvents[_index];
                    return new SensorBeginEvent
                    {
                        Sensor = new Shape(e.sensorShapeId),
                        Visitor = new Shape(e.visitorShapeId),
                    };
                }
            }

            /// <summary>Advances to the next event.</summary>
            /// <returns><see langword="true"/> while there are events remaining.</returns>
            public bool MoveNext() => ++_index < _events.beginCount;
        }
    }

    /// <summary>A read-only view over the sensor end-overlap events.</summary>
    public readonly ref struct SensorEndEventList
    {
        private readonly b3SensorEvents _events;

        internal SensorEndEventList(b3SensorEvents events) => _events = events;

        /// <summary>Gets the number of events.</summary>
        public int Count => _events.endCount;

        /// <summary>Gets an enumerator over the events.</summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator() => new(_events);

        /// <summary>Walks the sensor end-overlap events.</summary>
        public ref struct Enumerator
        {
            private readonly b3SensorEvents _events;
            private int _index;

            internal Enumerator(b3SensorEvents events)
            {
                _events = events;
                _index = -1;
            }

            /// <summary>Gets the event at the current position.</summary>
            public readonly unsafe SensorEndEvent Current
            {
                get
                {
                    ref readonly b3SensorEndTouchEvent e = ref _events.endEvents[_index];
                    return new SensorEndEvent
                    {
                        Sensor = new Shape(e.sensorShapeId),
                        Visitor = new Shape(e.visitorShapeId),
                    };
                }
            }

            /// <summary>Advances to the next event.</summary>
            /// <returns><see langword="true"/> while there are events remaining.</returns>
            public bool MoveNext() => ++_index < _events.endCount;
        }
    }

    /// <summary>A read-only view over the body move events.</summary>
    public readonly ref struct BodyMoveEventList
    {
        private readonly b3BodyEvents _events;

        internal BodyMoveEventList(b3BodyEvents events) => _events = events;

        /// <summary>Gets the number of events.</summary>
        public int Count => _events.moveCount;

        /// <summary>Gets an enumerator over the events.</summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator() => new(_events);

        /// <summary>Walks the body move events.</summary>
        public ref struct Enumerator
        {
            private readonly b3BodyEvents _events;
            private int _index;

            internal Enumerator(b3BodyEvents events)
            {
                _events = events;
                _index = -1;
            }

            /// <summary>Gets the event at the current position.</summary>
            public readonly unsafe BodyMoveEvent Current
            {
                get
                {
                    ref readonly b3BodyMoveEvent e = ref _events.moveEvents[_index];
                    return new BodyMoveEvent
                    {
                        Body = new Body(e.bodyId),
                        Position = e.transform.p,
                        Rotation = e.transform.q,
                        FellAsleep = e.fellAsleep,
                    };
                }
            }

            /// <summary>Advances to the next event.</summary>
            /// <returns><see langword="true"/> while there are events remaining.</returns>
            public bool MoveNext() => ++_index < _events.moveCount;
        }
    }
}
