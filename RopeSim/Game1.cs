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

public struct Stick(int start, int end, float distance)
{
    public int NodeAIndex = start;
    public int NodeBIndex = end;
    public readonly float Length = distance;
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
    private int _selectedNode = -1;

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
            _spriteBatch.DrawLine(_nodes[stick.NodeAIndex].Position, _nodes[stick.NodeBIndex].Position, Color.LightGray, StickThickness);
        }
        
        // Drawing Line From Selected Point
        if (_selectedNode > -1) _spriteBatch.DrawLine(_nodes[_selectedNode].Position, _prevMouseState.Position.ToVector2(), Color.LightGoldenrodYellow, StickThickness);
        
        // Drawing Nodes
        foreach (var node in _nodes)
        {
            var color = node.IsLocked ? Color.PaleVioletRed : Color.White;
            if (_selectedNode > -1) color = (_nodes[_selectedNode].ID == node.ID) ? Color.LightBlue : color;
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
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (!(Vector2.Distance(node.Position, mouseState.Position.ToVector2()) <
                      NodeRadius * 2 + NodeMargin)) continue;
                Console.WriteLine("Node Selected");
                _selectedNode = i;
                return;
            }

            // If there are no close nodes, create a new one
            _nodes.Add(new Node(mouseState.Position.ToVector2(), false));
        }
        else if (mouseState.LeftButton == ButtonState.Released && _prevMouseState.LeftButton == ButtonState.Pressed)
        {
            if (_selectedNode < 0) return;
            for (var i = 0; i < _nodes.Count; i++)
            {
                if (
                    i != _selectedNode &&
                    Vector2.Distance(_nodes[i].Position, mouseState.Position.ToVector2()) < NodeRadius * 2 + NodeMargin
                )
                {
                    Console.WriteLine("Stick Drawn");
                    _sticks.Add(
                        new Stick(
                            _selectedNode,
                            i,
                            Vector2.Distance(_nodes[_selectedNode].Position, _nodes[i].Position))
                    );
                    _selectedNode = -1;
                    return;
                }
            }

            // If there is no point present
            // Make a new point and make a connection between selected point and new point.
            _nodes.Add(new Node(mouseState.Position.ToVector2(), false));
            _sticks.Add(
                new Stick(_selectedNode,
                    _nodes.Count -1,
                    Vector2.Distance(_nodes[_selectedNode].Position, mouseState.Position.ToVector2())
                )
            );
            _selectedNode = -1;
        }
        else if (IsKeyJustPressed(Keys.LeftAlt, keyboardState))
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
            for (var i = 0; i < _sticks.Count; i++)
            {
                var stick = _sticks[i];
                var closestPoint = GetClosestPointToSegment(
                    mouseState.Position.ToVector2(),
                    _nodes[stick.NodeAIndex].Position,
                    _nodes[stick.NodeBIndex].Position
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
        
        
        var viewportWidth = _graphics.GraphicsDevice.Viewport.Width;
        var viewportHeight = _graphics.GraphicsDevice.Viewport.Height;

        // Point updating
        List<int> nodesToRemove = [];
        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            // Cleaning up nodes and sticks that are below the screen
            if (node.Position.Y > viewportHeight * 2)
            {
                List<int> cullList = [];
                for (var j = 0; j < _sticks.Count; j++)
                {
                    var stick = _sticks[j];
                    if (stick.NodeAIndex == i || stick.NodeBIndex == i)
                    {
                        cullList.Add(j);
                    }
                }
                // Sort the list so biggest
                cullList.Sort((a, b) => b.CompareTo(a));
                // Then remove them in order
                foreach (var index in cullList) _sticks.RemoveAt(index);

                nodesToRemove.Add(node.ID);
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
        nodesToRemove.Sort((a, b) => b.CompareTo(a));
        foreach (var node in nodesToRemove) _nodes.RemoveAt(node);
        
        for (var i = 0; i < NumberOfIterations; i++)
        {
            for (var j = 0; j < _sticks.Count(); j++)
            {
               
                var stick = _sticks[j];
                var nodeA = _nodes[stick.NodeAIndex];
                var nodeB = _nodes[stick.NodeBIndex];
                var stickCenter = (nodeA.Position + nodeB.Position) / 2;
                var stickDirection = Vector2.Normalize(nodeA.Position - nodeB.Position);

                if (!nodeA.IsLocked) nodeA.Position = stickCenter + stickDirection * stick.Length / 2;
                if (!nodeB.IsLocked) nodeB.Position = stickCenter - stickDirection * stick.Length / 2;

                _sticks[j] = stick;
                _nodes[stick.NodeAIndex] = nodeA;
                _nodes[stick.NodeBIndex] = nodeB;
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