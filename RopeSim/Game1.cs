using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Shapes;

namespace RopeSim;

public struct Node(Vector2 position, bool isLocked = true)
{
    private static int _idCount = 0;
    public int ID = ++_idCount;
    public Vector2 Position = position;
    public Vector2 PreviousPosition = position;
    public bool IsLocked = isLocked;
}

public struct Stick(Node start, Node end, float distance)
{
    public Node NodeA = start;
    public Node NodeB = end;
    public float Length = distance;
}

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private KeyboardState _prevKeyboardState;
    private MouseState _prevMouseState;

    // Simulation Properties
    private bool _isPaused = false;
    private const float Gravity = 0.98f;
    private const int NumberOfIterations = 5;
    private List<Node> _nodes = [];
    private List<Stick> _sticks = [];
    private Node? _selectedNode = null;

    // Stick Properties
    private const float StickThickness = 10.0f;
    private const float StickMargin = 10.0f;

    // Node Properties
    private const float NodeRadius = 10.0f;
    private const float NodeMargin = 5.0f;


    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        // TODO: Add your initialization logic here

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // TODO: use this.Content to load your game content here
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboardState = Keyboard.GetState();
        var mouseState = Mouse.GetState();
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();
        UpdateInput(keyboardState, mouseState);

        if (!_isPaused) UpdateSimulation(gameTime);

        _prevKeyboardState = keyboardState;
        _prevMouseState = mouseState;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.DimGray);

        // Drawing Sticks
        _spriteBatch.Begin();
        foreach (var stick in _sticks)
        {
            _spriteBatch.DrawLine(stick.NodeA.Position, stick.NodeB.Position, Color.LightGray, StickThickness);
        }
        
        // Drawing Line From Selected Point
        if (_selectedNode != null) _spriteBatch.DrawLine(_selectedNode.Value.Position, _prevMouseState.Position.ToVector2(), Color.LightGoldenrodYellow, StickThickness);
        
        // Drawing Nodes
        foreach (var node in _nodes)
        {
            var color = node.IsLocked ? Color.PaleVioletRed : Color.White;
            if (_selectedNode != null) color = (_selectedNode.Value.ID == node.ID) ? Color.LightBlue : color;
            _spriteBatch.DrawCircle(node.Position, NodeRadius, 36, color, NodeRadius * 2);
        }
        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void UpdateInput(KeyboardState keyboardState, MouseState mouseState)
    {
        if (IsKeyJustPressed(Keys.Space, keyboardState))
        {
            Console.WriteLine(_isPaused);
            _isPaused = !_isPaused;
        }

        if (mouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
        {
            // Try to find closest node
            foreach (var node in _nodes)
            {
                if (!(Vector2.Distance(node.Position, mouseState.Position.ToVector2()) <
                      NodeRadius * 2 + NodeMargin)) continue;
                Console.WriteLine("Node Selected");
                _selectedNode = node;
                return;
            }

            // If there are no close nodes, create a new one
            _nodes.Add(new Node(mouseState.Position.ToVector2(), false));
        }
        else if (mouseState.LeftButton == ButtonState.Released && _prevMouseState.LeftButton == ButtonState.Pressed)
        {
            if (_selectedNode == null) return;
            foreach (var node in _nodes)
            {
                if (
                    node.ID != _selectedNode.Value.ID &&
                    Vector2.Distance(node.Position, mouseState.Position.ToVector2()) < NodeRadius * 2 + NodeMargin
                )
                {
                    Console.WriteLine("Stick Drawn");
                    _sticks.Add(
                        new Stick(
                            _selectedNode.Value,
                            node,
                            Vector2.Distance(_selectedNode.Value.Position, node.Position))
                    );
                    _selectedNode = null;
                    return;
                }
            }

            // If there is no point present
            // Make a new point and make a connection between selected point and new point.
            _nodes.Add(new Node(mouseState.Position.ToVector2(), false));
            _sticks.Add(
                new Stick(_selectedNode.Value,
                    _nodes[^1],
                    Vector2.Distance(_selectedNode.Value.Position, mouseState.Position.ToVector2())
                )
            );
            _selectedNode = null;
        }
        else if (mouseState.MiddleButton == ButtonState.Pressed &&
                 _prevMouseState.MiddleButton == ButtonState.Released)
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (Vector2.Distance(node.Position, mouseState.Position.ToVector2()) < NodeRadius * 2 + NodeMargin)
                {
                    node.IsLocked = !node.IsLocked;
                    _nodes[i] = node;
                    return;
                }
            }
        }
        else if (keyboardState.IsKeyDown(Keys.LeftShift))
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                var stick = _sticks[i];
                var closestPoint = GetClosestPointToSegment(
                    mouseState.Position.ToVector2(),
                    stick.NodeA.Position,
                    stick.NodeB.Position
                );
                if (Vector2.Distance(mouseState.Position.ToVector2(), closestPoint) < StickMargin)
                {
                    Console.WriteLine("Removed Stick From Mouse");
                    _sticks.RemoveAt(i);
                }
            }
        }
    }

    private void UpdateSimulation(GameTime gameTime)
    {
        // No need to run sim if there are no points
        
        if (_nodes.Count < 1) return;
        Console.WriteLine("Sim Step");
        
        var viewportWidth = _graphics.GraphicsDevice.Viewport.Width;
        var viewportHeight = _graphics.GraphicsDevice.Viewport.Height;

        // Point updating
        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            // Cleaning up nodes and sticks that are below the screen
            if (node.Position.Y > viewportHeight * 2)
            {
                for (var j = 0; j < _sticks.Count; j++)
                {
                    var stick = _sticks[j];
                    if (stick.NodeA.ID == node.ID || stick.NodeB.ID == node.ID)
                    {
                        Console.WriteLine("Cleaned Up Stick");
                        _sticks.RemoveAt(j);
                    }
                }

                _nodes.RemoveAt(i);
                continue;
            }

            if (node.IsLocked) continue;

            // Moving the points
            var previousPosition = node.Position;

            node.Position += node.Position - node.PreviousPosition;
            node.Position += new Vector2(0, 1) * Gravity;

            node.PreviousPosition = previousPosition;
            _nodes[i] = node;
        }

        for (var i = 0; i < NumberOfIterations; i++)
        {
            for (var j = 0; j < _sticks.Count(); j++)
            {
                var stick = _sticks[j];
                var stickCenter = (stick.NodeA.Position + stick.NodeB.Position) / 2;
                var stickDirection = Vector2.Normalize(stick.NodeA.Position - stick.NodeB.Position);

                if (!stick.NodeA.IsLocked) stick.NodeA.Position = stickCenter + stickDirection * stick.Length / 2;
                if (!stick.NodeB.IsLocked) stick.NodeB.Position = stickCenter + stickDirection * stick.Length / 2;

                _sticks[j] = stick;
            }
        }
    }

    private bool IsKeyJustPressed(Keys key, KeyboardState keyboardState)
    {
        return (keyboardState.IsKeyDown(key) && _prevKeyboardState.IsKeyUp(key));
    }

    private Vector2 GetClosestPointToSegment(Vector2 point, Vector2 s1, Vector2 s2)
    {
        var segment = s2 - s1;
        var segmentLengthSquared = segment.LengthSquared();

        if (segmentLengthSquared == 0f) return s1;

        var t = Vector2.Dot(point - s1, segment) / segmentLengthSquared;
        t = MathHelper.Clamp(t, 0f, 1f);

        return s1 + t * segment;
    }
}