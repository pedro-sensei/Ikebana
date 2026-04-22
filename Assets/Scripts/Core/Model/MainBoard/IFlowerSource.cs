//=^..^=   =^..^=   VERSION 1.1.0 (April 2026)    =^..^=    =^..^=
//                    Last Update 21/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=

public interface IFlowerSource
{   
    bool IsFull { get; }
    bool IsEmpty { get; }
    int Capacity { get; }
    int Count { get; }
    //Returns an array of counts of flowers for each color. The index of the array corresponds to the flowerColor enum values.
    int[] GetColorCountsArray();
    // Returns the count of flowers of the specified color in the container.
    int GetflowerCountByColor(FlowerColor color);
    // Returns true if the flower was added successfully, false if the container is full or the flower is invalid.
    bool AddFlower(FlowerPiece flower);

    // Returns a flower if present. Removing it from the container. Returns null if no flower of the specified color is available.
    FlowerPiece TakeFlowerByColor(FlowerColor color);

    // Returns the first available flower. Removing it from the container. Returns null if the container is empty.
    FlowerPiece TakeFlower();
    
    // Returns an array of all flowers currently in the container.
    FlowerPiece[] GetAllFlowers();

    //Clears all flowers from the container.
    void Clear();

}
