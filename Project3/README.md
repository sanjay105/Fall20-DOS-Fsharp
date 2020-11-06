Distributed Operating Systems Project -3 - Implementation of Pastry Algorithm
----------------------------------------------------------------------------------------------------------------------------
Team Members:
-------------
    Deepthi Byneedi - 3687-1955
    Sanjay Reddy Banda - 5878-2239

Whats working?
--------------
    1. We are able to perform the join operation and updating the leaf nodes and routing tables whenever a new node joins the network. We will join all numNodes count nodes into the network by sending join message to one of 100 centralised nodes in the network.
    2. Join operation of each node is initialized by dispatcher which will send the join message of Node i to Node (i-1)%100. Now this node will add this node entry into it tables and sends the same join message to the node that is closer to the new nodes key. This runs until there is no closer node than itself.
    3. Once a node joins the network it starts sending the messages into the network by generating random node key for every second and stops sending message once it sent numRequests messages. This message is forwarded to the closest node to the destination. Each node forwards continously until there is no node that is closer than this, then it will send a response back the acknowledgement to the sender with the number of hops it made to reach destination.
    4. Once the node receives the response for all the messages sent with the number of hops taken to reach destination. We add up all the hop count and send back the total hop count to the dispatcher.
    5. Once the dispatcher received hopcounts from all the nodes it will find the average and displays the average.

Largest network that we managed to solve is with 500,000 nodes with numRequests as 100 and b = 4. Got the average hopratio of 2.894.
-
Command to run:
---------------
    dotnet fsi --langversion:preview pastry.fsx 'numNodes' 'numRequests'

Key Parameters:
---------------
    1. We have set b = 4 i.e. Key is of hexaDecimal and set the key length to be 7.
    2. There will be maximum 8 entries in both smallerleafset and largerleafset each.
    3. Routing table has 16 (2**b) columns and 7 rows.

Technically our program will able to handle the network of size of upper bound of 268,000,000 nodes but i was unable to simulate it because 500,000 nodes itself have taken approx 10hrs to finish the simulation.
-