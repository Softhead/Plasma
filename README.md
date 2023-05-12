# Plasma
Plasma DB

This is an experiment to implement a distributed database with sharding across up to 64K servers.  The idea is to have heavy redundance (configurable), along with self-healing and performance.

Uses the QUIC protocol, via a Microsoft library, for communication between servers.  When a client node needs to communicate with several storage nodes, all of the network requests are sent at once, to avoid waiting for the round trip from each storage node.  The implementation is fully asynchronous.

My inspiration for this is the Paxos protocol.  One of its shortcomings is that one storage node knows more about the status than other storage nodes.  I change that here and the storage nodes contain no status.  Only the client node knows the status.

NOTE:  I started with the C++ implementation, however I finished the C# implementation first.  Completing the C++ implementation is future work.
