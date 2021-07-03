# FileSync

The main goal of this project is to synchronize files in a folder between multiple machines. 

## How does it work?
If one of the clients gets changes, then these are reported to the service, which notifies all clients.
![image](images/concept.png)

## How to use?
- Download an client and an service from https://github.com/najlot/FileSync/releases
- Run the client and the service
- They both will tell you that they got no configuration in the config-folder and provide an example
- Remove the .example-suffix of the configuration files and modify the content to match your settings.
- Run the service and then the client.

## More questions?
Just create an issue at https://github.com/najlot/FileSync