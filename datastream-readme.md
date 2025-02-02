## Data stream system overview
This 'data stream system' enables sending "large" buffers with congestion control over the wire. This is useful if you need to serve data to clients where you would typically use TCP or a HTTP server.

Congestion control algorithm is custom, but it takes inspiration from TCP's congestion control. Here we use a notify protocol with timeouts to detect dropped packets.

The system ensure the receiver receives each buffer in the order they were sent. So if you call BeginStream with buffer A, and immediately after BeginStream with buffer B, OnStreamReceived will be always invoked with the buffer A first (even though buffer B might've been assembled and received first).

### How to use
- Call ``UdpServer.Tick(float dt)`` on the server periodically.
- Call ``UdpClient.Tick(float dt)`` on the client periodically.

Now when you want to stream a buffer, call 
``UdpConnection.BeginStream(int channel, byte[] buffer, int startIndex, int numBytes)``

This function call will return a handle and invoke event OnStreamDelivered with the handle, 
when the buffer has been delivered to the target.

On the client, event OnStreamReceived will be invoked with the sent buffer when the buffer is received.

### Room for improvement
There are couple places that could be improved upon on (in no specific order):
- last chunks (small) are not batched into one packet. it might not even be
    necessary since you would use this whole data streaming system
    to mainly stream large buffers, so the minimal gains might not
    be worth the efforts.
- only ensure order per 'channel'. right now everything is sent in the same channel, so
  for example if you have voice data and world state you are sending, sending either may delay
  receiving the other one unnecessarily.
- research whether acking and assembling out-of-order chunks is worth it.
- research optimization for streaming large buffers, even 10-50 MB?
- research remove unsafe code?
- research removing chunk timeouts and find an alternative approach to resend last chunks in a buffer.
  because we are using notify protocol, timeouts shouldn't be necessary.
- test and or implement if streaming from client to server works.
- research way to disconnect client if we cant deliver chunks?