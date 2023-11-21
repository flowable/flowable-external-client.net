namespace FlowableExternalWorkerClient.Rest;

public class Page<T>
{
    public List<T> Data { get; }
    public int Total { get; }
    public int Start { get; }
    public string? Sort { get; }
    public string? Order { get; }
    public int Size { get; }

    public Page(List<T> data, int total, int start, string? sort, string? order, int size)
    {
        Data = data;
        Total = total;
        Start = start;
        Sort = sort;
        Order = order;
        Size = size;
    }

    public override string ToString()
    {
        return
            $"{nameof(Data)}: {Data}, {nameof(Total)}: {Total}, {nameof(Start)}: {Start}, {nameof(Sort)}: {Sort}, {nameof(Order)}: {Order}, {nameof(Size)}: {Size}";
    }
}