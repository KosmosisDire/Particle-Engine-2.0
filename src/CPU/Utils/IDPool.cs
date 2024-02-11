

public class IDPool
{
    protected Queue<int> reusableIDs = new();
    protected int nextID;
    protected int capacity;
    protected bool reuseOldAtMaxCapacity;

    public IDPool(int maxCapacity, bool reuseOldAtMaxCapacity = false)
    {
        this.capacity = maxCapacity;
        this.reuseOldAtMaxCapacity = reuseOldAtMaxCapacity;
        nextID = 0;
    }

    public int NextID()
    {
        if (reusableIDs.Count > 0) return reusableIDs.Dequeue();
        
        if (nextID >= capacity)
        {
            if(reuseOldAtMaxCapacity) nextID = 0;
            else throw new Exception("Too many items in ID Pool");
        }

        return nextID++;
    }

    public void FreeID(int id)
    {
        reusableIDs.Enqueue(id);
    }

    public void Resize(int newCapacity)
    {
        if (newCapacity < capacity) throw new Exception("Cannot resize IDPool to a smaller capacity");
        capacity = newCapacity;
    }
}