[System.Serializable]

//=^..^=   =^..^=   VERSION 1.0.2 (April 2026)    =^..^=    =^..^=
//                    Last Update 01/04/2026 
//=^..^=    =^..^=  By Pedro Sánchez Vázquez      =^..^=    =^..^=
public class FlowerPiece
{
    #region FIELDS AND PROPERTIES
    public FlowerColor Color;
    public bool IsFirstPlayerToken;
    #endregion
    #region CONSTRUCTORS AND INITIALIZATION
    //Create a flower specifying color.
    public FlowerPiece(FlowerColor color)
        {
            Color = color;
            IsFirstPlayerToken = false;
            
            if (color == FlowerColor.FirstPlayer)
            {
                IsFirstPlayerToken = true;
            }

        }
        //Creates a flower piece that is the first player token. (factory Pattern)
        public static FlowerPiece CreateFirstPlayerToken()
        {
            FlowerPiece flower = new FlowerPiece(FlowerColor.FirstPlayer);
            flower.IsFirstPlayerToken = true;
            return flower;
        }

    #endregion
    #region GAME LOGIC 
    #endregion
    #region HELPER METHODS
    public override string ToString()
        {
            //TODO: when new special flowers are added, improve this.
            return $"[{Color}]";
        }
    #endregion
}