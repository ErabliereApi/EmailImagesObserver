# EmailImagesObserver

## Goal

Improve capabilities of an ip camera system.

## How it work

This app connect to an imap server and listen to new message sent. When there is one and the message have an attachment, if it is an image, the images is sent to azure cognitive service and store in the app folder (AppData\EmailImagesObserver\.).

## Pre-requisit

1. Azure cognitive service url
2. Azure cognitive service subscription key
3. Email address
4. Email password
5. Imap server address
6. Imap server port

> Information are store using the IDataProtector from Microsoft.AspNetCore.DataProtection

## Additionnal documentation

Mailkit: 
 - http://www.mimekit.net/docs/html/R_Project_Documentation.htm
 - https://github.com/jstedfast/MailKit

DataProtection : 
 - https://docs.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection
 - https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-5.0

Azure computer vision
 - https://docs.microsoft.com/en-us/azure/cognitive-services/computer-vision/quickstarts-sdk/client-library?tabs=visual-studio&pivots=programming-language-csharp