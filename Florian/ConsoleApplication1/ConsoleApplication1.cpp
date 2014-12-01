/*////////////
Heavily relies on code from : 
https://github.com/Kj1/ofxProjectorKinectCalibration
*/////////////////////


//#include <io.h>
#include <iostream>
#include <fstream>

#include <OpenNI.h>
#include "opencv2/highgui/highgui.hpp"
#include "opencv2/imgproc/imgproc.hpp"
#include "opencv2/calib3d/calib3d.hpp"
using namespace std;
using namespace cv;


//Projector / pattern values
Size projectorResolution(1024, 768);
bool reversed;
bool kinect;
cv::Mat projectedImg;
cv::Mat reconstructionImg;

vector<Point2f> foundCorners;
Size patternSize(6, 4);
int patternSquareWidth=30; 
Size patternPosition(100, 200); 
float worldUnitDiv=1000.0f;
int neededFramesNb=3;

vector<Point2f> generatedCorners;
int calibrating=0;

//Kinect variables / callbacks / thread

Size imageSize(640, 480);
unsigned int deviceID=0;

//CHESSBOARD
void generateChessboard(Mat& img,  Size& patternSize, 
                        int& patternSquareWidth, Size& patternPosition, 
                        vector<Point2f>& generatedCorners) {
    generatedCorners.clear();
    img.setTo(255);
    for(int j=0; j<patternSize.height+1; ++j) {
        int startY=patternSquareWidth*j+patternPosition.height;
        int i=j%2;
        for(; i<patternSize.width+1; i+=2) {
            int startX=patternSquareWidth*i+patternPosition.width;
            for(int px=0; px<patternSquareWidth; ++px) {
                for(int py=0; py<patternSquareWidth; ++py) {
                    if(startY+py>0 && startY+py<projectorResolution.height
                        && startX+px>0 && startX+px<projectorResolution.width) {
                            img.at<char>(startY+py, startX+px)=0;
                    }
                }
            }
        }
    }
    for(int j=1; j<patternSize.height+1; ++j) {
        for(int i=1; i<patternSize.width+1; ++i) {
            int cornerX=patternSquareWidth*i+patternPosition.width;
            int cornerY=patternSquareWidth*j+patternPosition.height;
            generatedCorners.push_back(Point2f(cornerX, cornerY));
        }
    }
}

static void cbPatternSquareWidth(int w, void*) {
    if(w>10) {
        patternSquareWidth=w;
        generateChessboard(projectedImg, patternSize, 
                            patternSquareWidth, patternPosition, 
                            generatedCorners);
    }
}

static void cbPatternPosX(int x, void*) {
    patternPosition.width=x;
    generateChessboard(projectedImg, patternSize, 
                        patternSquareWidth, patternPosition, 
                        generatedCorners);
}

static void cbPatternPosY(int y, void*) {
    patternPosition.height=y;
    generateChessboard(projectedImg, patternSize, 
                        patternSquareWidth, patternPosition, 
                        generatedCorners);
}

static void cbCalib(int c, void*) {
    calibrating=c;
}

//MAIN
int main(int argc, char* argv []) {

	//get projector resolution and kinect reversed from command line


	// Projector resolution
	Size projectorResolution(1024, 768);

	// Device Id
	deviceID = 0;

	// Up
	reversed = false;

	// We use kinect
	kinect = true;

	if (argc > 1) {
		neededFramesNb = atoi(argv[1]);
	}
    cout<<"Starting with resolution "<< projectorResolution
        <<" and device "<< deviceID<<" up kinect" << endl;

    projectedImg = Mat::zeros(projectorResolution, CV_8UC1);
    reconstructionImg = Mat::zeros(projectorResolution, CV_8UC3);

    //create windows and trackbars
    cv::Mat kinectRgbImg = Mat::zeros(imageSize, CV_8UC3);
	namedWindow("Projected", CV_WINDOW_NORMAL);  
	cvSetWindowProperty("Projected", CV_WND_PROP_FULLSCREEN, CV_WINDOW_FULLSCREEN);
    namedWindow("KinectRGB", CV_WINDOW_NORMAL);
    namedWindow("Controls", CV_WINDOW_NORMAL);
    
    createTrackbar("PatternSquareWidth", "Controls", 
                    &patternSquareWidth, 200, cbPatternSquareWidth);
    createTrackbar("Pattern Position X", "Controls", 
                    &patternPosition.width, 1920, cbPatternPosX);
    createTrackbar("Pattern Position Y", "Controls", 
                    &patternPosition.height, 1280, cbPatternPosY);
    createTrackbar("Calibrate", "Controls", 
                    &calibrating, 1, cbCalib);


    vector<vector<Point3f> > objectPoints;
    vector<vector<Point2f> > imagePoints;
    Mat cameraMatrix = Mat::eye(3, 3, CV_64F);
    Mat distCoeffs = Mat::zeros(8, 1, CV_64F);
    vector<Mat> rvecs;
    vector<Mat> tvecs;

    //generate the chessboard for the first time and display it
    generateChessboard(projectedImg, patternSize, 
                        patternSquareWidth, patternPosition, 
                        generatedCorners);
    imshow( "Projected", projectedImg);
    cvWaitKey(10);

    //start the depth cam
    openni::Status rc;
    openni::Device device;
    rc = openni::OpenNI::initialize();
    cout<<"Initialized OpenNI "<<openni::OpenNI::getExtendedError()<<endl;
    openni::Array<openni::DeviceInfo> deviceList;
    openni::OpenNI::enumerateDevices(&deviceList);
    rc = device.open(deviceList[deviceID].getUri());
    if(rc != openni::STATUS_OK) {
        cout<<"Could not open OpenNI device "<<deviceID<<endl;
    }

    //create streams
    openni::VideoStream depth, color; 
    openni::VideoFrameRef depthFrame;
    openni::VideoFrameRef colorFrame;
    rc = depth.create(device, openni::SENSOR_DEPTH);
    if(rc!=openni::STATUS_OK) {
        cout<<"Could not create depth stream"<<endl;
        return 0;
    }
    rc = color.create(device, openni::SENSOR_COLOR);
    if(rc!=openni::STATUS_OK) {
        cout<<"Could not create color stream"<<endl;
        return 0;
    }
    openni::VideoMode depthVideoMode;
    depthVideoMode.setResolution(imageSize.width, imageSize.height);
    depthVideoMode.setFps(30);
    depthVideoMode.setPixelFormat(openni::PIXEL_FORMAT_DEPTH_1_MM);
    rc = depth.setVideoMode(depthVideoMode);
    if(rc!=openni::STATUS_OK) {
        cout<<"Could not set depth video mode"<<endl;
    }
    openni::VideoMode colorVideoMode;
    colorVideoMode.setResolution(imageSize.width, imageSize.height);
    colorVideoMode.setFps(30);
    colorVideoMode.setPixelFormat(openni::PIXEL_FORMAT_RGB888);
    rc = color.setVideoMode(colorVideoMode);
    if(rc!=openni::STATUS_OK) {
        cout<<"Could not set color video mode"<<endl;
    }

    rc = depth.start();
    rc = color.start();
    if(!depth.isValid() || !color.isValid()) {
        cout<<"No valid streams"<<endl;
    }

    depth.setMirroringEnabled(false);
    color.setMirroringEnabled(false);


    openni::VideoStream** streams = new openni::VideoStream*[2];
    streams[0] = &depth;
    streams[1] = &color;

    device.setImageRegistrationMode(openni::IMAGE_REGISTRATION_DEPTH_TO_COLOR);
    device.setDepthColorSyncEnabled(true);

    //wait for calibration to be activated
    while(calibrating==0) {
        imshow("KinectRGB", kinectRgbImg);
        imshow("Projected", projectedImg);
        cvWaitKey(10);
    }

    bool gotDepth=false;
    bool gotColor=false;
    //loop until enough calibration images
    for(int f=0; f<neededFramesNb;) {
        //project the chessboard
        imshow( "Projected", projectedImg);

        int changedIndex;
        openni::OpenNI::waitForAnyStream(streams, 2, &changedIndex);
        switch(changedIndex) {
            case 0: {
                depth.readFrame(&depthFrame);
                gotDepth=true;
            } break;
            case 1: {
                color.readFrame(&colorFrame); 
                gotColor=true; 
            } break;
            default:break;
        }
    
        if(gotColor && gotDepth) {
            //copy to opencv mat
            for(int j=0; j<imageSize.height; ++j) {
                for(int i=0; i<imageSize.width; ++i) {
                    Vec3b& pix = kinectRgbImg.at<Vec3b>(j,i);
                    int coord=0;
                    if(reversed) {
                        //coord=(imageSize.height-1-j)*imageSize.width + i;
                        coord=(imageSize.height-1-j)*imageSize.width
                                + (imageSize.width-1-i);
                    }
                    else {
                        //coord=j*imageSize.width+(imageSize.width-1-i);
                        coord=j*imageSize.width+i;
                    }
                    openni::RGB888Pixel colorPix = 
                        ((openni::RGB888Pixel*) colorFrame.getData())[coord];
                    pix[0]=colorPix.r;
                    pix[1]=colorPix.g;
                    pix[2]=colorPix.b;
                }
            }

            imshow("KinectRGB", kinectRgbImg);

            foundCorners.clear();
            //find chessboard in image
            bool patternFound = findChessboardCorners(kinectRgbImg, 
                                                      patternSize, 
                                                      foundCorners, 
                                                  CV_CALIB_CB_ADAPTIVE_THRESH);
            //if we found it
            if(patternFound) {
                //refine
                Mat gray;
                cvtColor(kinectRgbImg, gray, CV_RGB2GRAY);
                cornerSubPix(gray, foundCorners, 
                             cv::Size(1, 1), cv::Size(-1, -1),
                             TermCriteria(CV_TERMCRIT_EPS + CV_TERMCRIT_ITER, 
                                            30, 0.1));


                bool validDepths=true;
                //retrieve the corresponding position and depth from the kinect
                vector<Point3f> worldCorners;
                vector<Point2f> kinectCorners;
                vector<Point2f>::iterator itPnt=foundCorners.begin(); 
                for(; itPnt!=foundCorners.end(); ++itPnt) {
                    float posX, posY, posZ;
                    if(reversed) {
                            kinectCorners.push_back(Point2f(
                                                imageSize.width-1-(*itPnt).x,
                                                imageSize.height-1-(*itPnt).y));
                    }
                    else {
                            kinectCorners.push_back(Point2f(
                                                (*itPnt).x, 
                                                (*itPnt).y));
                    }

                    openni::DepthPixel depthPix =
                        ((openni::DepthPixel*)depthFrame.getData())[int(
                                    kinectCorners.back().y*imageSize.width +
                                    kinectCorners.back().x)];

                    posZ=depthPix;

                    //filter the depth
                    int size=6;
                    vector<double> vals;
                    for(int x=kinectCorners.back().x-size/2; 
                                x<kinectCorners.back().x+size/2; ++x) {
                        for(int y=kinectCorners.back().y-size/2; 
                                y<kinectCorners.back().y+size/2; ++y) {
                            if(x>=0 && x<imageSize.width && 
                                    y>=0 && y<imageSize.height) {
                                vals.push_back(
                                ((openni::DepthPixel*)depthFrame.getData())
                                    [int(y*imageSize.width + x)]);
                            }
                        }
                    }
                    sort(vals.begin(), vals.end());
                    posZ=vals[vals.size()/2];
                    depthPix=posZ;

                    openni::CoordinateConverter::convertDepthToWorld(
                                                    depth, 
                                                    int(kinectCorners.back().x),
                                                    int(kinectCorners.back().y),
                                                    depthPix,
                                                    &posX, &posY, &posZ);
                    if(reversed) {
                        posX=-posX/worldUnitDiv;
                        posY=-posY/worldUnitDiv;
                    }
                    else {
                        posX=posX/worldUnitDiv;
                        posY=posY/worldUnitDiv;
                    }
                    posZ/=worldUnitDiv;
                    worldCorners.push_back(Point3f(posX, posY, posZ));
                    if(posZ<=500.0/worldUnitDiv || posZ>4000.0/worldUnitDiv) {
                        validDepths=false;
                    }
                }

                //if everything is valid
                if(validDepths) {
                    cout<<"Pattern found "<<f+1<<"/"<<neededFramesNb<<endl;
                    //add the generated corners
                    imagePoints.push_back(generatedCorners);
                    //add the detected world corners
                    objectPoints.push_back(worldCorners);

                    //draw corners 
                    drawChessboardCorners(kinectRgbImg, patternSize, 
                                            foundCorners, patternFound);
                    imshow("KinectRGB", kinectRgbImg);
                    
					cvWaitKey(1);
                    
					//wait 
					Sleep(4000);

                    //increase frame count
                    ++f;
                 }
            }
            gotDepth=false;
            gotColor=false;
        }
		cvWaitKey(1);
    }

    cout<<"Got all needed points"<<endl;
    cout<<"Calibrating ..."<<endl;

    //put the image and object points in the right order for opencv
	vector<vector<Point3f> > vvo(1); //object points
	vector<vector<Point2f> > vvi(1); //image points
	for (unsigned int i=0; i<objectPoints.size(); ++i) {
		for (unsigned int j = 0; j<objectPoints[i].size(); j++) {
			vvo[0].push_back(objectPoints[i][j]);
			vvi[0].push_back(imagePoints[i][j]);
		}
	}

    //when enough points found, get the extrinsics/intrisics parameter
    float h = projectorResolution.height;
    float w = projectorResolution.width;

    cameraMatrix = (Mat1d(3, 3) <<  w, 0, w/2.,
                                    0, h, h / 2.,
                                    0, 0, 1);
    distCoeffs = Mat::zeros(8, 1, CV_64F);
    //double rms=calibrateCamera(objectPoints, imagePoints,
    double rms=cvCalibrateCamera2(vvo, // object points 
								vvi, // image points
                                projectorResolution, // image size
								cameraMatrix, // cAMERA Mtrix
                                distCoeffs,  // 
								rvecs, 
								tvecs,
                                CV_CALIB_FIX_K1+
                                CV_CALIB_FIX_K2+
                                CV_CALIB_FIX_K3+
                                CV_CALIB_FIX_K4+
                                CV_CALIB_FIX_K5+
                                CV_CALIB_FIX_K6+
                                CV_CALIB_ZERO_TANGENT_DIST+
                                CV_CALIB_USE_INTRINSIC_GUESS);

    cout<<".. done , RMS="<<rms<<endl;

    //build the opengl view matrix
    Mat rot;
    rvecs[0].copyTo(rot);
    Mat rotMat = Mat::zeros(3, 3, CV_64F);
    rot.at<double>(0,0)*=1.0;
    rot.at<double>(0,1)*=1.0;
    rot.at<double>(0,2)*=1.0;
    Rodrigues(rot, rotMat);
    Mat viewMat = (Mat1d(4, 4) <<  
                rotMat.at<double>(0,0), rotMat.at<double>(0,1), 
                    rotMat.at<double>(0,2), tvecs[0].at<double>(0,0), 
                rotMat.at<double>(1,0), rotMat.at<double>(1,1),
                    rotMat.at<double>(1,2), tvecs[0].at<double>(0,1),
                rotMat.at<double>(2,0), rotMat.at<double>(2,1), 
                    rotMat.at<double>(2,2), tvecs[0].at<double>(0,2), 
               0 , 0, 0 , 1);
    Mat glCoordsMat = (Mat1d(4,4) << -1, 0, 0, 0, 
                                      0, 1, 0, 0,
                                      0, 0, 1, 0,
                                      0, 0, 0, 1);
    viewMat=glCoordsMat*viewMat;

    //build the opengl kinect to projector matrix
    rotMat=rotMat.inv();
    Mat invViewMat  = (Mat1d(4, 4) <<  
                rotMat.at<double>(0,0), rotMat.at<double>(0,1), 
                    rotMat.at<double>(0,2), -tvecs[0].at<double>(0,0), 
                rotMat.at<double>(1,0), rotMat.at<double>(1,1),
                    rotMat.at<double>(1,2), -tvecs[0].at<double>(0,1),
                rotMat.at<double>(2,0), rotMat.at<double>(2,1), 
                    rotMat.at<double>(2,2), -tvecs[0].at<double>(0,2), 
               0 , 0, 0 , 1);
    invViewMat=glCoordsMat*invViewMat;

    //build the opengl projection matrix
    double fx=cameraMatrix.at<double>(0,0);
    double cx=cameraMatrix.at<double>(0,2);
    double fy=cameraMatrix.at<double>(1,1);
    double cy=cameraMatrix.at<double>(1,2);
    double width=projectorResolution.width;
    double height=projectorResolution.height;
    double nearx=0.1;
    double farx=10;
    Mat projMat = (Mat1d(4,4) << 
                        2.0*fx/width, 0, 1.0-2.0*cx/width, 0,
                        0, 2.0*fy/height, -1.0+(2.0*cy+2.0)/height, 0,
                        0, 0, (farx+nearx)/(nearx-farx), (2.0*farx*nearx)/(nearx-farx),
                        0, 0, -1, 0);


    //write to file
    std::string outputFileName = "device_";
    cout<<"Writing results to "<<outputFileName<<endl;
    ostringstream oss;
    oss<<deviceID;
    outputFileName+=oss.str()+".txt";
    ofstream outputFile;
    
    outputFile.open(outputFileName.c_str());
    outputFile<<"Camera Matrix"<<endl;
    outputFile<<cameraMatrix<<endl;
    outputFile<<endl;
    outputFile<<"Projector translation"<<endl;
    outputFile<<tvecs[0]<<endl;
    outputFile<<endl;
    outputFile<<"Projector rotation"<<endl;
    outputFile<<rvecs[0]<<endl;
    outputFile<<endl;
    outputFile<<"OpenGL Projection Matrix"<<endl;
    outputFile<<projMat<<endl;
    outputFile<<endl;
    outputFile<<"OpenGL View Matrix"<<endl;
    outputFile<<viewMat<<endl;
    outputFile<<endl;
    outputFile.close();


    //project reconstructed kinect 
    while(1) {
        int changedIndex;
        openni::OpenNI::waitForAnyStream(streams, 2, &changedIndex);
        switch(changedIndex) {
            case 0: {
                depth.readFrame(&depthFrame);
                gotDepth=true;
            } break;
            case 1: {
                color.readFrame(&colorFrame); 
                gotColor=true; 
            } break;
            default:break;
        }
        if(gotColor && gotDepth) {

            //get points from the kinect and their actual 3D positions
            reconstructionImg.setTo(Vec3b(0,0,0));
            vector<Point3f> objPnts;
            vector<Vec3b> kinPnts;
            vector<Point2f> imgPnts;
            for(int j=0; j<imageSize.height; ++j) {
                for(int i=0; i<imageSize.width; ++i) {
                    float posX, posY, posZ;
                    int coord = imageSize.width*j+i;
                    openni::DepthPixel depthPix =
                        ((openni::DepthPixel*)depthFrame.getData())[coord];
                    openni::CoordinateConverter::convertDepthToWorld(
                                                        depth, 
                                                        i, j, depthPix,
                                                        &posX, &posY, &posZ);
                    if(reversed) {
                        posX=-posX/worldUnitDiv;
                        posY=-posY/worldUnitDiv;
                    }
                    else {
                        posX=posX/worldUnitDiv;
                        posY=posY/worldUnitDiv;
                    }
                    posZ/=worldUnitDiv;
                    
                    objPnts.push_back(Point3f(posX,posY,posZ));
                    const openni::RGB888Pixel& colorPix = 
                        ((openni::RGB888Pixel*) colorFrame.getData())[coord];
                    kinPnts.push_back(Vec3b(colorPix.r, 
                                            colorPix.g,
                                            colorPix.b));
                }
            }
            projectPoints(objPnts, rvecs[0], tvecs[0], 
                          cameraMatrix, distCoeffs, imgPnts);

            //for each of the image points
            vector<Point2f>::iterator itPnt=imgPnts.begin();
            vector<Vec3b>::iterator itKin=kinPnts.begin();
            vector<Point3f>::iterator itObj=objPnts.begin();
            for(; itPnt!=imgPnts.end() && itKin!=kinPnts.end(); 
                    ++itPnt, ++itKin, ++itObj) {
                if((*itPnt).x<reconstructionImg.cols && 
                        (*itPnt).y<reconstructionImg.rows &&
                        (*itPnt).x>0 &&
                        (*itPnt).y>0) {
                    reconstructionImg.at<Vec3b>(
                                    (*itPnt).y,
                                    (*itPnt).x)
                        =(*itKin);
                }
            }

			destroyWindow("Projected");

			namedWindow("Reconstruction", CV_WINDOW_NORMAL);
			moveWindow("Reconstruction", 2161, 0);
			cvSetWindowProperty("Reconstruction", CV_WND_PROP_FULLSCREEN, CV_WINDOW_FULLSCREEN);
            imshow( "Reconstruction", reconstructionImg);
            
            gotDepth=false;
            gotColor=false;
        }
        cvWaitKey(10);
        Sleep(1);
    }

    return EXIT_SUCCESS;
}


