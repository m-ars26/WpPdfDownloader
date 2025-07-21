using Google.Cloud.Firestore;
using System;
using System.Threading.Tasks;

public class FirestoreService
{
    private readonly FirestoreDb _firestoreDb;

    // JSON servis hesabı dosyasının tam yolu
    private const string CredentialPath = @"C:\Users\mehme\Desktop\json\config\whatsapppdfdownloader-firebase-adminsdk-fbsvc-e3db26d57e.json";

    // Firebase projenin ID’si  (Console > Project settings > General > Project ID)
    private const string ProjectId = "whatsapppdfdownloader";

    public FirestoreService()
    {
        // Servis hesabı kimliğini ortam değişkenine atıyoruz
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", CredentialPath);

        // Firestore istemcisini oluştur
        _firestoreDb = FirestoreDb.Create(ProjectId);
    }

    /// <summary>
    /// Cihaz ID'si Firestore'daki authorized_devices koleksiyonunda var mı?
    /// </summary>
    public async Task<bool> IsDeviceAuthorizedAsync(string deviceId)
    {
        try
        {
            DocumentReference docRef = _firestoreDb
                                        .Collection("authorized_devices")
                                        .Document(deviceId);

            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            // Belge varsa ve allowed==true ise izinli say
            if (snapshot.Exists &&
                snapshot.TryGetValue("allowed", out bool allowed) &&
                allowed)
            {
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Firestore hata: {ex.Message}");
            return false;
        }
    }
}
