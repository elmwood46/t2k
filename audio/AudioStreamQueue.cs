using Godot;
using System;
using System.Collections.Generic;

public partial class AudioStreamQueue : Node
{
    private int _num_channels;
    private AudioBus _bus;

    public AudioStreamQueue(int channels = 8, AudioBus bus = AudioBus.Master)
    {
        _num_channels = channels;
        _bus = bus;
    }

    private Queue<AudioStreamPlayer3D> _available = new();
    private Queue<(AudioStream,Vector3, float)> _queued_sounds = new();

    public override void _Ready()
    {
        // Create the pool of AudioStreamPlayer nodes.
        for (int i = 0; i < _num_channels; i++)
        {
            var player = new AudioStreamPlayer3D();
            AddChild(player);
            player.Bus = _bus.ToString();
            _available.Enqueue(player);
            player.Finished += () => _available.Enqueue(player);
        }
    }

    public void QueueSound(AudioStream stream, Vector3? position = null, float volumedb = 0.0f)
    {
        if (Player.Instance == null && position == null) return;
        var pos = position ?? Player.Instance.GlobalPosition;
        _queued_sounds.Enqueue((stream,pos, volumedb));
    }

    public void PlaySoundIfAvailable(AudioStream stream, Vector3? position = null, float volumedb = 0.0f)
    {
        if (Player.Instance == null && position == null) return;
        if (_available.Count > 0)
        {
            //GD.Print("playing sound on queue: ", this, "available channels: ", _available.Count);
            var player = _available.Dequeue();
            player.VolumeDb = volumedb;
            player.GlobalPosition = position ?? Player.Instance.GlobalPosition;
            player.Stream = stream;
            player.Play();
        }
    }

    public override void _Process(double delta)
    {
        // Play a queued sound if any players are _available.
        if (_queued_sounds.Count > 0 && _available.Count > 0)
        {
            var player = _available.Dequeue();
            var (stream, pos, volumedb) = _queued_sounds.Dequeue();
            player.Stream = stream;
            player.GlobalPosition = pos;
            player.VolumeDb = volumedb;
            player.Play();
        }
    }
}