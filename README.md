# Overview

This repo contains a port of the IronSHT system from IronFleet
(`https://github.com/microsoft/Ironclad/tree/main/ironfleet`)
to Verus. As such, this document and many of the files here are
modified versions of corresponding files in the IronFleet repo.

Note that this repository only verifies the "host program"
from IronFleet. IronFleet also proves that the host program
TLA state machine model, in the context of a distributed system,
implements the high-level spec. This repository doesn't replicate
that layer of proof, instead focusing on the implementation-level
reasoning.

# Setup

To use this repository, you'll need the following tools:
  * .NET 6.0 SDK (available at `https://dotnet.microsoft.com/download`)
  * python 2 or 3 (needed for running scons)
  * scons (installable by running `pip install scons`)
  * Verus (installable from `https://github.com/verus-lang/verus`)

# Dependencies

[Install Dotnet](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-2304)
```
sudo apt-get update && \
  sudo apt-get install -y aspnetcore-runtime-7.0
```

# Verification and Compilation

ironsht now relies on Verus's `main_new` branch

To verify the Verus contents of this repo and build the executables from the
contents of this repo, run:

  `scons --verus-path=<path_to_verus>`

where `<path_to_verus>` is the path to your local copy of the Verus repo;
this is where `scons` will look for `source/target/rust-verify.sh`.

To use `<n>` threads in parallel, add `-j <n>` to this command.

To skip verification when compiling Verus, use `--no-verify`.

Running `scons` will produce the following executables:
```
  liblib.so
  ironsht/bin/CreateIronServiceCerts.dll
  ironsht/bin/TestIoFramework.dll
  ironsht/bin/IronSHTServer.dll
  ironsht/bin/IronSHTClient.dll
```

# Running

## Creating certificates

Ironfleet servers identify themselves using certificates.  So, before running
any Ironfleet services, you need to generate certificates for the service by
running `CreateIronServiceCerts`.  On the command line you'll specify the name
and type of the service and, for each server, its public address and port.  Each
such address can be a hostname like `www.myservice.com` or an IP address like
`127.0.0.1` or `2001:db8:3333:4444:CCCC:DDDD:EEEE:FFFF`.

For instance, you can run the following command:
```
  dotnet ironsht/bin/CreateIronServiceCerts.dll outputdir=certs name=MyService type=TestService addr1=server1.com port1=6000 addr2=server2.com port2=7000
```
This will create three files in the directory `certs`.  Two of these files,
`MyService.TestService.server1.private.txt` and
`MyService.TestService.server2.private.txt`, are the private key files for the
two servers.  The third, `MyService.TestService.service.txt`, contains the
service identity, including the public keys of the two servers.

You'll distribute the service file to all servers and all clients.  But,
you should only copy a private key file to the server corresponding to that
private key, and after copying it you should delete your local copy.  So, in
this example, you'd copy `MyService.TestService.server1.private.txt` only to
server1.com.

## IronSHT

To run IronSHT (our sharded hash table), you should ideally use multiple
different machines, but in a pinch you can use separate windows on the same
machine.

The client has reasonable defaults that you can override with key=value
command-line arguments. Run the client with no arguments to get detailed usage
information. Make sure your firewall isn't blocking the TCP ports you use.

To test the IronSHT sharded hash table on a single machine, you can do the following.
First, create certificates with:
```
  dotnet ironsht/bin/CreateIronServiceCerts.dll outputdir=certs name=MySHT type=IronSHT addr1=127.0.0.1 port1=4001 addr2=127.0.0.1 port2=4002 addr3=127.0.0.1 port3=4003
```

Then, run each of the following three server commands, each in a different window:
```
  dotnet ironsht/bin/IronSHTServer.dll certs/MySHT.IronSHT.service.txt certs/MySHT.IronSHT.server1.private.txt
  dotnet ironsht/bin/IronSHTServer.dll certs/MySHT.IronSHT.service.txt certs/MySHT.IronSHT.server2.private.txt
  dotnet ironsht/bin/IronSHTServer.dll certs/MySHT.IronSHT.service.txt certs/MySHT.IronSHT.server3.private.txt
```

Finally, run this client command in yet another window:
```
  dotnet ironsht/bin/IronSHTClient.dll certs/MySHT.IronSHT.service.txt nthreads=10 duration=30 workload=g numkeys=1000
```
The client's output will primarily consist of reports of the form `#req
<thread-ID> <request-number> <time-in-ms>`.

We haven't implemented crash recovery, so if you restart a server its state will
be empty.
```

# Contributing

See the [CONTRIBUTING](./CONTRIBUTING.md) file for more details.

# Version History
- v0.1:  Initial code release
