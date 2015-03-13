# SimpleMail
An email service written in ASP.NET and hosted on Microsoft Azure.

This weekend, I took a crack at [Uber's coding challenge] (https://github.com/uber/coding-challenge-tools/blob/master/coding_challenge.md).

From all the options, I chose the __back-end track__ for the __Email service__ (#3 in the list) and named it __SimpleMail__. Let's get straight into it.

## Requirements

Since the project implementation was open to interpretation (except one key requirement) I made a list to structure the effort.

####Functional:
- User can send mail(s) using SimpleMail
- The UI is simple and *presentable* (i.e. some CSS, javacript)
- Malformed email requests are rejected with appropriate, human-readable error messages

####Technical:
- [__key__] *Failover*. SimpleMail should provide an abstraction between two (or more) email providers like Amazon SES, SendGrid etc.
- *Scalability*. SimpleMail should be built with scale-out in mind. It should be relatively painless to add more machines into the mix without requiring too much re-work.
- *Security*. Users need to sign-up for SimpleMail (or use an external authentication provider like Facebook or Google) to send emails.
  - Initially I didn't feel this was important, but for any production-worthy service you need some form of security.
- *Rate Limiting*. Misbehaving senders spamming inboxes is not great use of the service.
- *Performance*. SimpleMail should be fast. Of course.

Based on the requirements alone it is easy to notice that I leaned towards the back-end architecture and was eager to showcase some learnings over the last few years (and revise!).

## Technology Choices

- I chose ASP.NET and C# over __Python__.
- I hosted the project on Microsoft Azure and instead of __Amazon__ or __Heroku__.

> Reason: I'm pretty familiar with ASP.NET and Microsoft Azure.

I wanted to express my ideas freely (without having to rely on tutorials and stackoverflow too much) and engineer in the most optimized way possible (for me personally). 
I don't have knowledge of multi-threading, asynchronous programming, generics and other advanced concepts in Python/Javascript. Also C# is pretty similar to Java in terms of readability.

__That said my next project is to port this to Python!__

## Implementation

ASP.NET + Azure projects come with a decent bit of boilerplate code.
So I've moved all the interesting stuff I had to write on my own to:
- Heavy-lifting around table/blob operations and ServiceBus: [SimpleMail/SimpleMail.Library.Storage](https://github.com/felix8/SimpleMail/tree/master/SimpleMail.Library.Storage)
- Web Role/Controller code: [SimpleMail/SimpleMail.Web/Controllers/EmailController.cs](https://github.com/felix8/SimpleMail/blob/master/SimpleMail.Web/Controllers/EmailController.cs)
- Worker Roles: [SimpleMail/SimpleMail.SendGrid.Worker/WorkerRole.cs](https://github.com/felix8/SimpleMail/blob/master/SimpleMail.SendGrid.Worker/WorkerRole.cs)

The design of the system was built around jobs, workers and queues. In order of execution they are:

#### Front-End (Browser):
- A user logs into SimpleMail by creating an account or through Facebook Auth
  - Stored passwords are stored in encrypted format in SqlAzure.
- User completes a multi-part form that represents an email and clicks "Send"
  - Using javascript + html5 we do some basic checks here such as valid email addresseses, mandatory fields etc.

#### API Layer (Web Roles):
- An Azure web role instance receives an __authenticated__ request and performs further validation, otherwise returns the Login view.
  - Web roles sit behind the Azure load balancer which can scale with the service.
- [*could not implement, lack of time*] If the sender has a bad reputation (built from feedback from email provider - they have APIs for this) the web role either rejects the request or perfoms rate-limiting.
- The web role then writes email metadata (to, cc, bcc, message and subject) to persistent storage (NoSql - Azure Table Storage)
- If attachments are present they are uploaded to Blob storage (Azure Blob Storage)
- The web role then pushes a message (or __Job__) into an enterprise queue (in-order and reliable delivery guarantees)

#### Back-End (Workers):
- Workers are on the other end of the queue listening for Jobs
- A job can only go to one worker (in the current configuration) i.e. jobs have a 1:1 relationship with workers
- On receiving the job, a worker first checks if the mail has already been sent (we track this by a "Sent" field in the NoSql table storage)
  - This is possible if the worker crashes before being able to send the mail.
- If the email has not been sent, the worker composes an email request by reading Table Storage and then downloading attachments
- It then sends the mail to its designated email provider i.e. each worker is mapped to either Amazon or SendGrid.

> Here's a block diagram explaining the process.

![SimpleMail Design](https://github.com/felix8/SimpleMail/blob/master/SimpleMail.Web/Content/Pictures/SimpleMail.png "SimpleMail Initial Design")

## Pitfalls

#### Deduplication:

SimpleMail doesn't guarantee this. An example:
  - A worker receives a job i.e. it takes a lease (form of lock) on a message in the queue
  - It composes the mail and sends it to the email provider (Amazon, SendGrid) successfully
  - It crashes before it can:
    - update table storage to set "Sent" flag to true
    - tell the queue server to remove the message from the queue
  - After the lease expires, the queue server sends the same message to another listening worker that sends the email again.

Could I have enforced deduplication? At what cost? I had fun thinking about it.

#### Compute resources wasted when email provider is down:

Since I've allocated one worker per email provider. If one email provider goes down (e.g. SendGrid) the worker that is assigned to it will get stuck the following loop:
- Read a job off the queue
- Compose email and send
- Fail
- Try again

#### Error Handling around Table and Blob Storage

These APIs are well documented and return a number of error codes based on transient failures. My error handling around cloud storage was pretty basic and could do with a little more detail.

This will continue until I manually shut it down or SendGrid bans incoming requests from me. There is no intelligence baked in today to learn that a provider is unavailable (or doesn't want worker to retry until x seconds). An exponential retry would be useful to implement here.

#### Few automated tests
- You can't get into production without them!
- I did a lot of manual testing though
- Will add some more soon!

I'll add more pitfalls as I think of them.

#### Final Notes

Thanks for reading and I would love to hear feedback!
Arijit.
