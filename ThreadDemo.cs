using Godot;
using System;

public partial class ThreadDemo : Node
{
    GodotThread thread = new();
    Semaphore semaphore = new();
    Mutex mutex = new ();

    public void Activate() {
        thread.Start(new Callable(this,nameof(SimpleThread)));
    }

    private void SimpleThread(string userdata) {
        while (true) {
            GD.Print("Thread is running, userdata is ", userdata);
            //thread.Sleep(1000);
        }
    }

    public override void _ExitTree()
    {
        //thread.Abort();
    }
}
