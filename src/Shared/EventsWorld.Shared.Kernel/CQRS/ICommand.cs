namespace EventsWorld.Shared.Kernel.CQRS;

public interface ICommand;

public interface ICommand<out TResult>;
