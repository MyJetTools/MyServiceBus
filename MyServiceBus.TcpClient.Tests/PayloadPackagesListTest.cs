using NUnit.Framework;

namespace MyServiceBus.TcpClient.Tests
{
    public class PayloadPackagesListTest
    {
        
        /// <summary>
        /// We are trying to get next payload package. First time it's available. Second time - it's not
        /// </summary>
        [Test]
        public void BasicTests()
        {
            var payloadPackages = new PayloadPackagesList(1024, new GetNextRequestId().GetNextId);

            var payloadPackage = payloadPackages.GetNextPayloadPackageToPost();
            
            payloadPackage.Add(new byte[] {1,2,3}, false);

            var nextPayloadPackage = payloadPackages.GetNextPayloadPackageToDeliver();
            
            Assert.AreEqual(1, nextPayloadPackage.PayLoads.Count);
            
            nextPayloadPackage = payloadPackages.GetNextPayloadPackageToDeliver();
            
            Assert.IsNull(nextPayloadPackage);
        }

        /// <summary>
        /// Check if we are trying to commit the package which does not exist in the list
        /// </summary>
        [Test]
        public void CommitTestWithEmptyResult()
        {
            var payloadPackages = new PayloadPackagesList(1024, new GetNextRequestId().GetNextId);

            var result = payloadPackages.TryToCommit(1);
            
            Assert.IsNull(result);
        }
        
        /// <summary>
        /// We are publishing and commiting 
        /// </summary>
        [Test]
        public void CommitAfterPublish()
        {
            var payloadPackages = new PayloadPackagesList(1024, new GetNextRequestId().GetNextId);

            var payloadPackage = payloadPackages.GetNextPayloadPackageToPost();
            
            payloadPackage.Add(new byte[] {1,2,3}, false);

            var packageToDeliver = payloadPackages.GetNextPayloadPackageToDeliver();

            var committedPayloadPackage = payloadPackages.TryToCommit(packageToDeliver.RequestId);
            
            Assert.IsNotNull(committedPayloadPackage);
            Assert.AreEqual(0, payloadPackages.Count);
        }


        /// <summary>
        /// Test - if we are trying to push more data than one package can fit
        /// </summary>
        [Test]
        public void FillSeveralPackagesAndTryingToPublish()
        {
            var payloadPackages = new PayloadPackagesList(3, new GetNextRequestId().GetNextId);

            var payloadPackage = payloadPackages.GetNextPayloadPackageToPost();
            
            Assert.AreEqual(1, payloadPackage.RequestId);
            
            payloadPackage.Add(new byte[] {1,2,3}, false);

            payloadPackage = payloadPackages.GetNextPayloadPackageToPost();
            
            Assert.AreEqual(2, payloadPackage.RequestId);
            
            var packageToDeliver = payloadPackages.GetNextPayloadPackageToDeliver();
            
            Assert.IsNotNull(packageToDeliver);

            packageToDeliver = payloadPackages.GetNextPayloadPackageToDeliver();
            
            Assert.IsNull(packageToDeliver);

        }


        [Test]
        public void CheckIfWeFillTwoPackagesSecondStaysAfterCommit()
        {
            var payloadPackagesList = new PayloadPackagesList(3, new GetNextRequestId().GetNextId);

            var payloadPackage = payloadPackagesList.GetNextPayloadPackageToPost();
            
            Assert.AreEqual(1, payloadPackage.RequestId);
            
            payloadPackage.Add(new byte[] {1,2,3}, false);

            payloadPackage = payloadPackagesList.GetNextPayloadPackageToPost();
            
            payloadPackage.Add(new byte[] {4,5,6}, false);
            
            Assert.AreEqual(2, payloadPackage.RequestId);
            
            Assert.AreEqual(2, payloadPackagesList.Count);
            
            var packageToDeliver = payloadPackagesList.GetNextPayloadPackageToDeliver();

            payloadPackagesList.TryToCommit(packageToDeliver.RequestId);
            
            Assert.AreEqual(1, payloadPackagesList.Count);

        }
        
    }


    public class GetNextRequestId
    {
        private long _nextId;
        public long GetNextId()
        {
            _nextId++;
            return _nextId;
        }
    }
}