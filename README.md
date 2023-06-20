# ZhongHong-HomeAssistant MQTT Bridge

A simple service which bridges ZhongHong VRF controller with HomeAssistant via MQTT. Basically it does two things:
    - pulls data from ZhongHong controller and publishes to MQTT broker with HomeAssistant-recognizable topics
    - subscribes the topics on MQTT broker (where HomeAssistant pubs the control message) and sends instructions to ZhongHong controller

## Usage

This is a containerized service. Either build with docker locally and run, or pull from ghcr and run. The configration is (usually) required, and the simplest way is to use an env file.

Put the following contents in `env` file or whatever you want to name it:
```bash
ZhongHong__GatewayUrl=http://10.0.0.2 # Your ZhongHong VRF controller URL
ZhongHong__UserName=admin # ZhongHong default username
ZhongHong__Password= # ZhongHong default password
Mqtt__Broker=10.0.0.1 # Your MQTT broker address
Mqtt__HasCredential=true # false if your MQTT broker allows annoymous
Mqtt__Username=mqtt_username
Mqtt__Password=mqtt_password
```

And then run interactively:
```
docker run -it --env-file=env zhonghong-hass-bridge
```

