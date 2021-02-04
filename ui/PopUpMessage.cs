using Godot;
using System;

public class PopUpMessage : Popup
{

    public class QueueMessage : Godot.Object
    {
        public String Body { get; set; }
        public String Title { get; set; }
        public float Time { get; set; }
    }

    private Timer _popUpMessageTimer;
    private RichTextLabel _messageName;
    private RichTextLabel _messageBody;

    private Godot.Collections.Array<QueueMessage> _messages;

    public override void _Ready()
    {
        _popUpMessageTimer = (Timer)GetNode("ColorRect/PopUpMessageTimer");
        _messageName = (RichTextLabel)GetNode("ColorRect/MessageName");
        _messageBody = (RichTextLabel)GetNode("ColorRect/MessageBody");

        _messages = new Godot.Collections.Array<QueueMessage>();
    }

    public void NotifyMessage(String title, String body, float time = 2)
    {
        QueueMessage message = new QueueMessage();
        message.Title = title;
        message.Body = body;
        message.Time = time;
        _messages.Add(message);
    }

    private void _processMessage()
    {
        if (_popUpMessageTimer.IsStopped() && _messages.Count > 0)
        {
            QueueMessage message = _messages[0];

            _messageName.BbcodeText = message.Title;
            _messageBody.BbcodeText = message.Body;
            _popUpMessageTimer.WaitTime = message.Time;

            _messages.RemoveAt(0);

            this.Show();
            _popUpMessageTimer.Start();
        }
    }

    public void MessageTimerTimeout()
    {
        this.Hide();
    }

    public override void _Process(float delta)
    {
        _processMessage();
    }
}
