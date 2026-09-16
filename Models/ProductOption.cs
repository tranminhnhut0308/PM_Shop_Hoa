namespace ShopHoa.Models;

public sealed class ProductOption
{
    public uint Id { get; init; }
    public string Name { get; init; } = "";

    public override string ToString() => Name;
}
