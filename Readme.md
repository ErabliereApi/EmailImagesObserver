# EmailImagesObserver

## Goal

Improve capabilities of an ip camera system.

Compatible email services

- office 365
- gmail

## How it work

This app connect to an imap server and listen to new message sent. When there is one and the message have an attachment, if it is an image, the images is sent to azure cognitive service and result are store in an EF repository of choices.

![Diagram](Docs/EmailImageObserver.drawio.png)

## Pre-requisit

1. Azure cognitive service url
2. Azure cognitive service subscription key
3. Email address
4. Email password
5. Imap server address
6. Imap server port

## Run the app

```
dotnet watch run
```

Local url are:
 - http://localhost:5100
 - https://localhost:5101

## Deploy the app

There is a docker image availlable on dockerhub. Here is an example of deployment behind reverse proxy that use many configuration.

https://github.com/freddycoder/AzureAKS-TLS/blob/main/emailimagesobserver/emailimagesobserver-deployment.yaml

## Additionnal documentation

Mailkit: 
 - http://www.mimekit.net/docs/html/R_Project_Documentation.htm
 - https://github.com/jstedfast/MailKit

Azure computer vision
 - https://docs.microsoft.com/en-us/azure/cognitive-services/computer-vision/quickstarts-sdk/client-library?tabs=visual-studio&pivots=programming-language-csharp
