namespace Moz.Events
{

    public interface IEventPublisher
    {
        void Publish<T>(T eventMessage);
    }
}