using Microsoft.Extensions.Options;
using NotificationManagementSystem.Data;
using NotificationManagementSystem.Helpers;
using NotificationManagementSystem.Models;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace NotificationManagementSystem.Services
{
    public class MessageService : IMessageService
    {
        private readonly MessageSettings _messageSettings;
        private readonly SendGridSettings _sendGridSettings;
        private readonly TwilioSettings _twilioSettings;
        private readonly IMessageRepository _messageRepository;

        public MessageService(IOptions<MessageSettings> messageSettings, 
            IOptions<SendGridSettings> sendGridSettings, 
            IOptions<TwilioSettings> twilioSettings, IMessageRepository messageRepository)
        {
            _messageSettings = messageSettings.Value;
            _sendGridSettings = sendGridSettings.Value;
            _twilioSettings = twilioSettings.Value;
            _messageRepository = messageRepository;
        }

        private SendGridMessage CreateMessage(MessageRequest message)
        {
            if (string.IsNullOrWhiteSpace(message.From))
            {
                message.From = _sendGridSettings.Email;
                message.SenderName = _sendGridSettings.Name;
            }

            var msg = new SendGridMessage()
            {
                From = new EmailAddress(message.From, message.SenderName),
                Subject = message.Subject,
                PlainTextContent = message.Body,
                HtmlContent = message.Body
            };


            msg.AddTo(new EmailAddress(message.To));
            return msg;
        }

        public async Task<bool> SendEmailAsync(MessageRequest message)
        {
            var status = false;
            try
            {
                var msg = CreateMessage(message);
                var client = new SendGridClient(_sendGridSettings.ApiKey);
                var retries = _messageSettings.Retries;   //retries is set to 5 in the appsettings.json file

                //Retry 5 times
                while (!status && retries > 0)
                {
                    var response = await client.SendEmailAsync(msg);
                    status = response.StatusCode == System.Net.HttpStatusCode.Accepted;
                    retries--;
                }
                return status;
            }

            //no to retry if the catch block is reached.
            catch
            {
                return status;
            }
        }

        public bool SendSms(MessageRequest message)
        {
            var status = false;
            try
            {
                TwilioClient.Init(_twilioSettings.AccountsId, _twilioSettings.AuthToken);

                //retries is set to 5 in the appsettings.json file
                var retries = _messageSettings.Retries;

                //format phone number 
                string phone = message.To.Substring(1, 10);
                string completephonenumber = "+234" + phone;

                //Retry 5 times 
                while (!status && retries > 0)
                {
                    var messageResponse = MessageResource.Create(
                        body: "Message from " + message.From + ": " +message.Body,
                        from: new Twilio.Types.PhoneNumber($"{_twilioSettings.Phone}"),
                        to: new Twilio.Types.PhoneNumber($"{completephonenumber}")
                    );
                    status = messageResponse.ErrorCode == null && messageResponse.ErrorMessage == null;
                    retries--;
                }
                return status;
            }
            // No need to retry if the catch block is reached.
            catch (Exception ex) 
            {
                return status;
            }
        }

        public List<Message> GetAllMessages()
        {
            return _messageRepository.GetAllMessages().ToList();
        }

        public List<Message> GetMessagesByStatus(bool? status)
        {
            return _messageRepository.GetMessagesByStatus(status).ToList();
        }

        public void AddMessage(Message newMessage)
        {
            _messageRepository.AddEntity(newMessage);
            _messageRepository.SaveAll();
        }
    }
}
