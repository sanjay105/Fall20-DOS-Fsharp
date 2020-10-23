Distributed Operating Systems Project 2 - Gossip Simulator
------------------------------------------------------------------------------------------------------------------------
Team Members-

Sanjay Reddy Banda 58782239
Deepthi Byneedi 36871955	

------------------------------------------------------------------------------------------------------------------------
How to run-
1. Go inside the project directory using: cd project2
2. To run, use: dotnet fsi --langversion:preview project2.fsx numNodes topology algorithm
Where numNodes is the number of nodes 
topology can be any of the four - full, 2D, line,imp2D
algorithm is either gossip or push-sum

------------------------------------------------------------------------------------------------------------------------
What is working-

Convergence of Gossip algorithm for all topologies - Full, Line, 2D, Improper 2D
Convergence of Push Sum algorithm for all topologies - Full, Line, 2D,Improper 2D


------------------------------------------------------------------------------------------------------------------------
Largest Network - 

Number of Nodes for Gossip Algorithm- 
Full topology: 5000
Line topology : 10000
2D topology: 10000
Improper 2D: 10000

Number of Nodes for Push Sum Algorithm-

Full topology: 10000
Line topology: 2000
2D topology: 5000
Improper 2D: 10000

------------------------------------------------------------------------------------------------------------------------
We might sometimes observe that full topology takes additional amount of time compared to a line, as in a line all the nodes cannot converge but in a full network we see that most of the nodes get the rumor so they run until everyone has heard it until 10 times.
Hence full takes extra time to converge sometimes.
