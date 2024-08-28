namespace MAXI_X3.Models
{
    internal class Article
    {
        public int Age { get; set; }
        public int Count { get; set; }
        public string Name { get; set; }

        public string DodeDepot { get; set; }
        public string RefArticle { get; set; }
        public string Designation { get; set; }
        public string CodeFamille { get; set; }
        public string UniteVente { get; set; }
        public string PrixAchat { get; set; }
        public string PrixVentHT { get; set; }

        public Article(int age, int count, string name)
        {
            Age = age;
            Count = count;
            Name = name;
        }

        public Article(string refArticle, string designation, string codeFamille, string uniteVente, string prixAchat, string prixVentHT)
        {
            RefArticle = refArticle;
            Designation = designation;
            CodeFamille = codeFamille;
            UniteVente = uniteVente;
            PrixAchat = prixAchat;
            PrixVentHT = prixVentHT;
        }

        public Article()
        {
        }

        public override string ToString()
        {
            return string.Format("{0};{1};{2};{3};{4};{5}", RefArticle, Designation, CodeFamille, UniteVente, PrixAchat, PrixVentHT);
        }
    }
}
